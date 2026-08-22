using System.IO;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginRepositoryServiceTests : IDisposable
{
    private const string SourceCommit =
        "0123456789abcdef0123456789abcdef01234567";
    private const string CatalogCommit =
        "89abcdef0123456789abcdef0123456789abcdef";

    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), $"akasha-repository-{Guid.NewGuid():N}");
    private readonly Mock<ILogService> _logService = new();

    [Fact]
    public async Task InitializeAsync_UsesValidCacheWithoutSynchronizing()
    {
        var repositoryDirectory = GetRepositoryDirectory();
        WriteIndex(repositoryDirectory, CreateIndex());
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit
        };
        var service = CreateService(gitClient);

        var result = await service.InitializeAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.UsedCache);
        Assert.Equal(CatalogCommit, result.Value.CatalogCommit);
        Assert.Equal(0, gitClient.SynchronizeCount);
    }

    [Fact]
    public async Task RefreshAsync_SynchronizesAndPublishesFreshSnapshot()
    {
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit
        };
        gitClient.Synchronize = (_, _, directory, _, _) =>
        {
            WriteIndex(directory, CreateIndex());
            return Task.FromResult(CatalogCommit);
        };
        var service = CreateService(gitClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.UsedCache);
        Assert.Same(result.Value, service.Current);
        Assert.Equal(AppConstants.OfficialPluginRepositoryGitHubUrl, gitClient.RepositoryUrl);
        Assert.Equal(AppConstants.OfficialPluginRepositoryBranch, gitClient.Branch);
    }

    [Fact]
    public async Task RefreshAsync_WhenSynchronizationFails_ReturnsCachedSnapshot()
    {
        WriteIndex(GetRepositoryDirectory(), CreateIndex());
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit,
            Synchronize = (_, _, _, _, _) =>
                Task.FromException<string>(new IOException("network unavailable"))
        };
        var service = CreateService(gitClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.UsedCache);
        Assert.Equal(CatalogCommit, result.Value.CatalogCommit);
        _logService.Verify(
            logger => logger.Warn(
                nameof(PluginRepositoryService),
                It.IsAny<string>(),
                It.IsAny<object?[]>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAsync_WhenSynchronizationFails_DoesNotUseCache()
    {
        WriteIndex(GetRepositoryDirectory(), CreateIndex());
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit,
            Synchronize = (_, _, _, _, _) =>
                Task.FromException<string>(new IOException("network unavailable"))
        };
        var service = CreateService(gitClient);

        var result = await service.ResetAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("PLUGIN_REPOSITORY_SYNC_FAILED", result.Error!.Code);
        Assert.True(gitClient.Reset);
    }

    [Fact]
    public async Task RefreshAsync_RejectsDuplicateOrUnsortedPluginIds()
    {
        var invalidIndex = CreateIndex();
        invalidIndex.Plugins.Add(CreateEntry("sample-plugin"));
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit
        };
        gitClient.Synchronize = (_, _, directory, _, _) =>
        {
            WriteIndex(directory, invalidIndex);
            return Task.FromResult(CatalogCommit);
        };
        var service = CreateService(gitClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("PLUGIN_REPOSITORY_ENTRY_INVALID", result.Error!.Code);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task RefreshAsync_RejectsNullPluginCollectionWithoutThrowing()
    {
        var invalidIndex = CreateIndex();
        invalidIndex.Plugins = null!;
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit
        };
        gitClient.Synchronize = (_, _, directory, _, _) =>
        {
            WriteIndex(directory, invalidIndex);
            return Task.FromResult(CatalogCommit);
        };
        var service = CreateService(gitClient);

        var result = await service.RefreshAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("PLUGIN_REPOSITORY_INDEX_INVALID", result.Error!.Code);
    }

    [Fact]
    public void SaveSettings_ValidatesAndPersistsRepositorySource()
    {
        var service = CreateService(new FakePluginRepositoryGitClient());
        var invalidResult = service.SaveSettings(
            new PluginRepositorySettings {
                SelectedChannel = PluginRepositoryChannel.Custom,
                CustomUrl = "http://example.com/plugins.git"
            });

        Assert.True(invalidResult.IsFailure);
        Assert.Equal("PLUGIN_REPOSITORY_URL_INVALID", invalidResult.Error!.Code);

        var settings = new PluginRepositorySettings {
            SelectedChannel = PluginRepositoryChannel.Cnb,
            AutoUpdateRepository = false,
            AutoUpdateSubscribedPlugins = true,
            AutoUpdatePluginResources = false
        };
        var saveResult = service.SaveSettings(settings);

        Assert.True(saveResult.IsSuccess);
        var json = File.ReadAllText(GetSettingsFilePath());
        var persisted = JsonSerializer.Deserialize<PluginRepositorySettings>(
            json,
            JsonHelper.ReadOptions);
        Assert.Equal(PluginRepositoryChannel.Cnb, persisted!.SelectedChannel);
        Assert.False(persisted.AutoUpdateRepository);
        Assert.True(persisted.AutoUpdateSubscribedPlugins);
        Assert.False(persisted.AutoUpdatePluginResources);

        settings.SelectedChannel = PluginRepositoryChannel.GitHub;
        var returnedSettings = service.Settings;
        returnedSettings.SelectedChannel = PluginRepositoryChannel.Custom;
        Assert.Equal(PluginRepositoryChannel.Cnb, service.Settings.SelectedChannel);
    }

    [Fact]
    public async Task RefreshAsync_SerializesConcurrentRepositoryWrites()
    {
        WriteIndex(GetRepositoryDirectory(), CreateIndex());
        var activeCalls = 0;
        var maximumActiveCalls = 0;
        var gitClient = new FakePluginRepositoryGitClient {
            HeadCommit = CatalogCommit
        };
        gitClient.Synchronize = async (_, _, _, _, cancellationToken) =>
        {
            var active = Interlocked.Increment(ref activeCalls);
            InterlockedExtensions.Max(ref maximumActiveCalls, active);
            await Task.Delay(25, cancellationToken);
            Interlocked.Decrement(ref activeCalls);
            return CatalogCommit;
        };
        var service = CreateService(gitClient);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => service.RefreshAsync()));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, maximumActiveCalls);
        Assert.Equal(4, gitClient.SynchronizeCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private PluginRepositoryService CreateService(
        IPluginRepositoryGitClient gitClient)
    {
        return new PluginRepositoryService(
            _logService.Object,
            gitClient,
            GetRepositoryDirectory(),
            GetSettingsFilePath());
    }

    private string GetRepositoryDirectory()
    {
        return Path.Combine(_testDirectory, "repository");
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(_testDirectory, "plugin-repositories.json");
    }

    private static PluginRepositoryIndex CreateIndex()
    {
        return new PluginRepositoryIndex {
            SchemaVersion = 1,
            Commit = SourceCommit,
            Plugins = new List<PluginRepositoryEntry> {
                CreateEntry("sample-plugin")
            }
        };
    }

    private static PluginRepositoryEntry CreateEntry(string id)
    {
        return new PluginRepositoryEntry {
            Id = id,
            Path = $"plugins/{id}",
            Name = "Sample Plugin",
            Version = "1.0.0",
            Description = "Sample description",
            DistributionType = "repository",
            MinHostVersion = "1.4.0"
        };
    }

    private static void WriteIndex(
        string repositoryDirectory,
        PluginRepositoryIndex index)
    {
        var result = JsonHelper.SaveToFile(
            Path.Combine(repositoryDirectory, AppConstants.PluginRepositoryIndexFileName),
            index);
        Assert.True(result.IsSuccess);
    }

    private sealed class FakePluginRepositoryGitClient : IPluginRepositoryGitClient
    {
        public Func<string, string, string, bool, CancellationToken, Task<string>>
            Synchronize { get; set; } =
                (_, _, _, _, _) => Task.FromResult(CatalogCommit);

        public string? HeadCommit { get; set; }

        public int SynchronizeCount { get; private set; }

        public string? RepositoryUrl { get; private set; }

        public string? Branch { get; private set; }

        public bool Reset { get; private set; }

        public Task<string> SynchronizeAsync(
            string repositoryUrl,
            string branch,
            string repositoryDirectory,
            bool reset,
            CancellationToken cancellationToken)
        {
            SynchronizeCount++;
            RepositoryUrl = repositoryUrl;
            Branch = branch;
            Reset = reset;
            return Synchronize(
                repositoryUrl,
                branch,
                repositoryDirectory,
                reset,
                cancellationToken);
        }

        public string? GetHeadCommit(string repositoryDirectory)
        {
            return HeadCommit;
        }
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            var current = Volatile.Read(ref location);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(
                    ref location,
                    value,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
