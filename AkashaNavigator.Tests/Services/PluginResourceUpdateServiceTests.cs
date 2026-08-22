using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using AkashaNavigator.Tests.TestDoubles;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginResourceUpdateServiceTests : IDisposable
{
    private const string PluginId = "resource-test-plugin";
    private const string ResourceId = "default-list";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"akasha-plugin-resources-{Guid.NewGuid():N}");

    [Fact]
    public async Task Update_ShouldVerifyAndAtomicallyInstallSubscribedResource()
    {
        var payload = Encoding.UTF8.GetBytes("[\"one\",\"two\"]");
        var fixture = CreateService(payload, payload);

        var result = await fixture.Service.UpdateSubscribedResourcesAsync();

        Assert.True(result.IsSuccess);
        var update = Assert.Single(result.Value!);
        Assert.True(update.Succeeded);
        Assert.True(update.Updated);
        var current = Path.Combine(_root, PluginId, ResourceId, $"{Sha256(payload)}.json");
        Assert.Equal(payload, File.ReadAllBytes(current));
        var state = fixture.Service.GetInstalledState(PluginId, ResourceId);
        Assert.NotNull(state);
        Assert.Equal(Sha256(payload), state!.Sha256);
        Assert.Equal($"{Sha256(payload)}.json", state.FileName);
    }

    [Fact]
    public async Task CorruptDownload_ShouldPreserveLastKnownGoodResource()
    {
        var good = Encoding.UTF8.GetBytes("[\"known-good\"]");
        var corrupt = Encoding.UTF8.GetBytes("[\"corrupt\"]");
        var fixture = CreateService(good, corrupt);
        var first = await fixture.Service.UpdateSubscribedResourcesAsync();
        Assert.True(Assert.Single(first.Value!).Succeeded);

        var updated = Encoding.UTF8.GetBytes("[\"new-version\"]");
        WriteCatalog(fixture.RepositoryDirectory, updated, "2.0.0");
        var second = await fixture.Service.UpdateSubscribedResourcesAsync();

        var failure = Assert.Single(second.Value!);
        Assert.False(failure.Succeeded);
        var current = Path.Combine(_root, PluginId, ResourceId, $"{Sha256(good)}.json");
        Assert.Equal(good, File.ReadAllBytes(current));
        Assert.Equal(Sha256(good), fixture.Service.GetInstalledState(PluginId, ResourceId)!.Sha256);
    }

    [Fact]
    public async Task NewSourceVersionWithSameContent_ShouldRefreshStateMetadata()
    {
        var payload = Encoding.UTF8.GetBytes("[\"unchanged\"]");
        var fixture = CreateService(payload, payload);
        var first = await fixture.Service.UpdateSubscribedResourcesAsync();
        Assert.True(Assert.Single(first.Value!).Updated);
        WriteCatalog(fixture.RepositoryDirectory, payload, "2.0.0");

        var second = await fixture.Service.UpdateSubscribedResourcesAsync();

        Assert.True(Assert.Single(second.Value!).Updated);
        Assert.Equal(
            "2.0.0",
            fixture.Service.GetInstalledState(PluginId, ResourceId)!.SourceVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Fixture CreateService(byte[] firstResponse, byte[] subsequentResponse)
    {
        var repositoryDirectory = Path.Combine(_root, "catalog");
        Directory.CreateDirectory(repositoryDirectory);
        WriteCatalog(repositoryDirectory, firstResponse, "1.0.0");
        var snapshot = new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = new string('a', 40),
                Plugins =
                [
                    new PluginRepositoryEntry {
                        Id = PluginId,
                        Path = $"plugins/{PluginId}",
                        Name = PluginId,
                        Version = "1.0.0",
                        Description = "test",
                        DistributionType = "release",
                        HasBackend = true,
                        MinHostVersion = "1.4.2"
                    }
                ]
            },
            new string('a', 40),
            UsedCache: true);

        var repository = new Mock<IPluginRepositoryService>();
        repository.SetupGet(service => service.RepositoryDirectory).Returns(repositoryDirectory);
        repository.Setup(service => service.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(snapshot));

        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions.Setup(service => service.GetSubscriptions()).Returns(
        [
            new PluginSubscriptionRecord {
                PluginId = PluginId,
                RepositoryId = "official",
                RepositoryPath = $"plugins/{PluginId}",
                IsAvailable = true
            }
        ]);
        subscriptions.Setup(service => service.IsSubscribed(PluginId)).Returns(true);

        var installed = new InstalledPluginInfo { Id = PluginId, Version = "1.0.0" };
        var library = new Mock<IPluginLibrary>();
        library.Setup(service => service.GetInstalledPlugins()).Returns([installed]);
        library.Setup(service => service.GetInstalledPluginInfo(PluginId)).Returns(installed);

        var selector = new Mock<IDownloadSourceSelector>();
        selector.Setup(
                service => service.GetOrderedSourcesAsync(
                    It.IsAny<PluginPackageInfo>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<DownloadSourceInfo>>.Success(
                [
                    new DownloadSourceInfo { Id = "test", Url = "https://example.invalid/resource" }
                ]));
        var handler = new SequenceHandler(firstResponse, subsequentResponse);
        var service = new PluginResourceUpdateService(
            repository.Object,
            subscriptions.Object,
            library.Object,
            selector.Object,
            new HttpClient(handler),
            new FakeLogService(),
            _root);
        return new Fixture(service, repositoryDirectory);
    }

    private static void WriteCatalog(
        string repositoryDirectory,
        byte[] expectedPayload,
        string sourceVersion)
    {
        var digest = Sha256(expectedPayload);
        var directory = Path.Combine(repositoryDirectory, "plugins", PluginId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "resources.json"),
            JsonHelper.Serialize(
                new PluginResourceCatalog {
                    PluginId = PluginId,
                    Resources =
                    [
                        new CatalogPluginResource {
                            Id = ResourceId,
                            Revision = digest,
                            SourceVersion = sourceVersion,
                            Distribution = new CatalogPluginDistribution {
                                Type = "release",
                                Tag = $"resource-{digest[..12]}",
                                Asset = $"list.{digest[..12]}.json",
                                Size = expectedPayload.Length,
                                Sha256 = digest
                            }
                        }
                    ]
                }));
    }

    private static string Sha256(byte[] payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private sealed record Fixture(
        PluginResourceUpdateService Service,
        string RepositoryDirectory);

    private sealed class SequenceHandler(params byte[][] responses) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var index = Math.Min(Interlocked.Increment(ref _index) - 1, responses.Length - 1);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(responses[index])
                });
        }
    }
}
