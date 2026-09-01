using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class InstalledPluginsPageViewModelTests
{
    [Fact]
    public async Task OnLoadedAsync_UsesSubscriptionCatalogUpdates()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    new[] {
                        new PluginSubscriptionUpdate {
                            PluginId = "sample-plugin",
                            InstalledVersion = "1.0.0",
                            AvailableVersion = "2.0.0"
                        }
                    }));
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out _);

        await viewModel.OnLoadedAsync();

        var plugin = Assert.Single(viewModel.Plugins);
        Assert.True(plugin.HasUpdate);
        Assert.Equal("2.0.0", plugin.AvailableVersion);
        subscriptions.Verify(
            service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnLoadedAsync_MovesAvailableUpdatesBeforeOtherPlugins()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    new[] {
                        new PluginSubscriptionUpdate {
                            PluginId = "zulu-plugin",
                            InstalledVersion = "1.0.0",
                            AvailableVersion = "2.0.0"
                        }
                    }));
        var installed = new List<InstalledPluginInfo> {
            new() {
                Id = "alpha-plugin",
                Name = "Alpha Current",
                Version = "1.0.0"
            },
            new() {
                Id = "zulu-plugin",
                Name = "Zulu Update",
                Version = "1.0.0"
            }
        };
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out _,
            installed);

        await viewModel.OnLoadedAsync();

        Assert.Equal("zulu-plugin", viewModel.Plugins[0].Id);
        Assert.True(viewModel.Plugins[0].HasUpdate);
        Assert.False(viewModel.Plugins[1].HasUpdate);
    }

    [Fact]
    public async Task UpdatePluginCommand_UsesRepositoryInstaller()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    Array.Empty<PluginSubscriptionUpdate>()));
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out var acquisition);
        acquisition
            .Setup(service => service.InstallOrUpdateAsync(
                "sample-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "2.0.0"
                    }));

        await viewModel.UpdatePluginCommand.ExecuteAsync("sample-plugin");

        acquisition.Verify(
            service => service.InstallOrUpdateAsync(
                "sample-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePluginCommand_ShowsProgressAndDisablesOtherUpdates()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.CheckForUpdatesAsync(
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<IReadOnlyList<PluginSubscriptionUpdate>>.Success(
                    Array.Empty<PluginSubscriptionUpdate>()));
        var completion = new TaskCompletionSource<Result<InstalledPluginInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = CreateViewModel(
            subscriptions.Object,
            out var acquisition);
        acquisition
            .Setup(service => service.InstallOrUpdateAsync(
                "sample-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(completion.Task);

        var operation = viewModel.UpdatePluginCommand.ExecuteAsync("sample-plugin");

        Assert.True(viewModel.IsPluginUpdateBusy);
        Assert.True(viewModel.IsPluginUpdateProgressIndeterminate);
        Assert.Contains("Sample", viewModel.PluginUpdateStatusText);
        Assert.False(viewModel.UpdatePluginCommand.CanExecute("other-plugin"));

        completion.SetResult(
            Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo {
                    Id = "sample-plugin",
                    Name = "Sample",
                    Version = "2.0.0"
                }));
        await operation;

        Assert.False(viewModel.IsPluginUpdateBusy);
        Assert.True(viewModel.UpdatePluginCommand.CanExecute("other-plugin"));
    }

    private static InstalledPluginsPageViewModel CreateViewModel(
        IPluginSubscriptionService subscriptions,
        out Mock<IPluginAcquisitionService> acquisition,
        IReadOnlyList<InstalledPluginInfo>? installedPlugins = null)
    {
        var library = new Mock<IPluginLibrary>();
        library
            .Setup(service => service.GetInstalledPlugins())
            .Returns(
                installedPlugins?.ToList() ??
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "sample-plugin",
                        Name = "Sample",
                        Version = "1.0.0"
                    }
                });
        library
            .Setup(service => service.GetInstalledPluginInfo("sample-plugin"))
            .Returns(
                new InstalledPluginInfo {
                    Id = "sample-plugin",
                    Name = "Sample",
                    Version = "1.0.0"
                });
        var associations = new Mock<IPluginAssociationManager>();
        associations
            .Setup(service => service.GetPluginReferenceCount(
                It.IsAny<string>()))
            .Returns(0);
        associations
            .Setup(service => service.GetProfilesUsingPlugin(
                It.IsAny<string>()))
            .Returns(new List<string>());
        acquisition = new Mock<IPluginAcquisitionService>();
        return new InstalledPluginsPageViewModel(
            library.Object,
            associations.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEventBus>(),
            subscriptions,
            acquisition.Object);
    }
}
