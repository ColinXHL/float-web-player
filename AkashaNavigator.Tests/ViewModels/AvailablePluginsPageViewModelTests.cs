using System.IO;
using System.Windows;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class AvailablePluginsPageViewModelTests
{
    private const string CatalogCommit =
        "89abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void RefreshPluginList_UsesRepositoryIndexAsCatalogAuthority()
    {
        var snapshot = CreateSnapshot(usedCache: true);
        var repositoryService = CreateRepositoryService(snapshot);
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "alpha-plugin",
                        Name = "Old Alpha",
                        Version = "1.0.0"
                    }
                });
        var viewModel = CreateViewModel(
            repositoryService.Object,
            pluginLibrary.Object);

        viewModel.RefreshPluginList();

        Assert.Equal(2, viewModel.Plugins.Count);
        var alpha = Assert.Single(
            viewModel.Plugins.Where(plugin => plugin.Id == "alpha-plugin"));
        Assert.Equal("2.0.0", alpha.Version);
        Assert.True(alpha.IsInstalled);
        Assert.True(alpha.HasUpdate);
        Assert.Equal(
            AppConstants.PluginDistributionRepository,
            alpha.DistributionType);
        Assert.Equal(
            Path.Combine("C:\\catalog-cache", "plugins/alpha-plugin"),
            alpha.SourceDirectory);
        var beta = Assert.Single(
            viewModel.Plugins.Where(
                plugin => plugin.Id == "beta-plugin"));
        Assert.True(beta.IsCatalogDistribution);
        Assert.Equal(
            Visibility.Visible,
            beta.SubscribeButtonVisibility);
    }

    [Fact]
    public void RefreshPluginList_MovesAvailableUpdatesBeforeOtherPlugins()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        snapshot.Index.Plugins[0].Name = "Zulu Update";
        snapshot.Index.Plugins[1].Name = "Alpha Current";
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(
                new List<InstalledPluginInfo> {
                    new() {
                        Id = "alpha-plugin",
                        Name = "Zulu Update",
                        Version = "1.0.0"
                    },
                    new() {
                        Id = "beta-plugin",
                        Name = "Alpha Current",
                        Version = "1.0.0"
                    }
                });
        var viewModel = CreateViewModel(
            CreateRepositoryService(snapshot).Object,
            pluginLibrary.Object);

        viewModel.RefreshPluginList();

        Assert.Equal("alpha-plugin", viewModel.Plugins[0].Id);
        Assert.True(viewModel.Plugins[0].HasUpdate);
        Assert.False(viewModel.Plugins[1].HasUpdate);
    }

    [Fact]
    public async Task OnLoadedAsync_ReportsCachedRepositoryAndDoesNotRefreshLegacyManifest()
    {
        var snapshot = CreateSnapshot(usedCache: true);
        var repositoryService = CreateRepositoryService(snapshot);
        repositoryService
            .Setup(service => service.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(snapshot));
        var viewModel = CreateViewModel(repositoryService.Object);

        await viewModel.OnLoadedAsync();

        Assert.Contains("本地缓存", viewModel.RepositoryStatusText);
        Assert.Contains("2 个插件", viewModel.RepositoryStatusText);
        repositoryService.Verify(
            service => service.InitializeAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRepositoryCommand_SavesChannelThenRefreshesCatalog()
    {
        var cached = CreateSnapshot(usedCache: true);
        var fresh = CreateSnapshot(usedCache: false);
        var current = cached;
        PluginRepositorySettings? savedSettings = null;
        var repositoryService = CreateRepositoryService(cached);
        repositoryService
            .SetupGet(service => service.Current)
            .Returns(() => current);
        repositoryService
            .Setup(service => service.SaveSettings(
                It.IsAny<PluginRepositorySettings>()))
            .Callback<PluginRepositorySettings>(settings => savedSettings = settings)
            .Returns(Result.Success());
        repositoryService
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .Callback(() => current = fresh)
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(fresh));
        var viewModel = CreateViewModel(repositoryService.Object);
        viewModel.SelectedRepositoryChannel = PluginRepositoryChannel.Cnb;
        viewModel.AutoUpdateRepository = false;

        await viewModel.UpdateRepositoryCommand.ExecuteAsync(null);

        Assert.Equal(PluginRepositoryChannel.Cnb, savedSettings!.SelectedChannel);
        Assert.False(savedSettings.AutoUpdateRepository);
        Assert.Contains("最新仓库", viewModel.RepositoryStatusText);
        repositoryService.Verify(
            service => service.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallCommand_UsesUnifiedInstallerForRepositoryPlugin()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var repositoryService = CreateRepositoryService(snapshot);
        var installer = new Mock<IPluginInstaller>();
        installer
            .Setup(service => service.InstallOrUpdateRepositoryPluginAsync(
                "alpha-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = "alpha-plugin",
                        Name = "Alpha",
                        Version = "2.0.0"
                    }));
        var viewModel = CreateViewModel(
            repositoryService.Object,
            installer: installer.Object);
        viewModel.RefreshPluginList();
        var plugin = Assert.Single(
            viewModel.Plugins.Where(item => item.Id == "alpha-plugin"));

        await viewModel.InstallCommand.ExecuteAsync(plugin);

        Assert.True(plugin.IsInstalled);
        Assert.True(plugin.IsSubscribed);
        Assert.False(plugin.HasUpdate);
        installer.Verify(
            service => service.InstallOrUpdateRepositoryPluginAsync(
                "alpha-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallCommand_UsesCatalogInstallerForReleasePlugin()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var installer = new Mock<IPluginInstaller>();
        installer
            .Setup(service => service.InstallOrUpdateRepositoryPluginAsync(
                "beta-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = "beta-plugin",
                        Name = "Beta",
                        Version = "1.0.0"
                    }));
        var viewModel = CreateViewModel(
            CreateRepositoryService(snapshot).Object,
            installer: installer.Object);
        viewModel.RefreshPluginList();
        var plugin = Assert.Single(
            viewModel.Plugins.Where(
                item => item.Id == "beta-plugin"));

        await viewModel.InstallCommand.ExecuteAsync(plugin);

        Assert.True(plugin.IsInstalled);
        Assert.True(plugin.IsSubscribed);
        installer.Verify(
            service => service.InstallOrUpdateRepositoryPluginAsync(
                "beta-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallCommand_ShowsIndeterminateInstallStateAndDisablesOtherInstalls()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var completion = new TaskCompletionSource<Result<InstalledPluginInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var acquisition = new Mock<IPluginAcquisitionService>();
        acquisition
            .Setup(service => service.InstallOrUpdateAsync(
                "alpha-plugin",
                null,
                It.IsAny<CancellationToken>()))
            .Returns(completion.Task);
        var viewModel = CreateViewModel(
            CreateRepositoryService(snapshot).Object,
            acquisitionService: acquisition.Object);
        viewModel.RefreshPluginList();
        var plugin = Assert.Single(
            viewModel.Plugins.Where(item => item.Id == "alpha-plugin"));
        var otherPlugin = Assert.Single(
            viewModel.Plugins.Where(item => item.Id == "beta-plugin"));

        var operation = viewModel.InstallCommand.ExecuteAsync(plugin);

        Assert.True(viewModel.IsPluginInstallBusy);
        Assert.True(plugin.IsInstalling);
        Assert.True(plugin.IsInstallProgressIndeterminate);
        Assert.Contains("正在安装", plugin.InstallStatus);
        Assert.False(viewModel.InstallCommand.CanExecute(otherPlugin));

        completion.SetResult(
            Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo {
                    Id = "alpha-plugin",
                    Name = "Alpha",
                    Version = "2.0.0"
                }));
        await operation;

        Assert.False(viewModel.IsPluginInstallBusy);
        Assert.False(plugin.IsInstalling);
        Assert.True(viewModel.InstallCommand.CanExecute(otherPlugin));
    }

    [Fact]
    public void SubscribeAndUnsubscribeCommands_OnlyChangeSubscriptionState()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var repositoryService = CreateRepositoryService(snapshot);
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.Subscribe(
                AppConstants.OfficialPluginRepositoryId,
                It.Is<PluginRepositoryEntry>(
                    entry => entry.Id == "alpha-plugin")))
            .Returns(
                Result<PluginSubscriptionRecord>.Success(
                    new PluginSubscriptionRecord {
                        PluginId = "alpha-plugin"
                    }));
        subscriptions
            .Setup(service => service.Unsubscribe("alpha-plugin"))
            .Returns(Result.Success());
        subscriptions
            .Setup(service => service.Reconcile(
                It.IsAny<string>(),
                It.IsAny<PluginRepositorySnapshot>()))
            .Returns(Result.Success());
        subscriptions
            .Setup(service => service.GetSubscriptions())
            .Returns(Array.Empty<PluginSubscriptionRecord>());
        var library = CreatePluginLibrary();
        var viewModel = CreateViewModel(
            repositoryService.Object,
            library.Object,
            subscriptionService: subscriptions.Object);
        viewModel.RefreshPluginList();
        var plugin = Assert.Single(
            viewModel.Plugins.Where(item => item.Id == "alpha-plugin"));

        viewModel.SubscribeCommand.Execute(plugin);
        Assert.True(plugin.IsSubscribed);
        viewModel.UnsubscribeCommand.Execute(plugin);

        Assert.False(plugin.IsSubscribed);
        library.Verify(
            service => service.UninstallPlugin(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Func<string, List<string>>?>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAllSubscribedCommand_InstallsEveryAvailableUpdate()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var subscriptions = CreateSubscriptionService();
        subscriptions
            .Setup(service => service.GetAvailableUpdates())
            .Returns(
                new List<PluginSubscriptionUpdate> {
                    new() {
                        PluginId = "alpha-plugin",
                        InstalledVersion = "1.0.0",
                        AvailableVersion = "2.0.0",
                        AutoUpdate = true
                    }
                });
        var installer = new Mock<IPluginInstaller>();
        installer
            .Setup(service => service.InstallOrUpdateRepositoryPluginAsync(
                "alpha-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<InstalledPluginInfo>.Success(
                    new InstalledPluginInfo {
                        Id = "alpha-plugin",
                        Name = "Alpha",
                        Version = "2.0.0"
                    }));
        var viewModel = CreateViewModel(
            CreateRepositoryService(snapshot).Object,
            subscriptionService: subscriptions.Object,
            installer: installer.Object);

        await viewModel.UpdateAllSubscribedCommand.ExecuteAsync(null);

        installer.Verify(
            service => service.InstallOrUpdateRepositoryPluginAsync(
                "alpha-plugin",
                It.IsAny<IProgress<PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RefreshPluginList_ShowsRemovedSubscriptionWithoutInstallAction()
    {
        var snapshot = CreateSnapshot(usedCache: false);
        var subscriptions = CreateSubscriptionService();
        subscriptions
            .Setup(service => service.GetSubscriptions())
            .Returns(
                new List<PluginSubscriptionRecord> {
                    new() {
                        PluginId = "removed-plugin",
                        RepositoryId = AppConstants.OfficialPluginRepositoryId,
                        RepositoryPath = "plugins/removed-plugin",
                        LastKnownVersion = "1.0.0",
                        IsAvailable = false
                    }
                });
        var viewModel = CreateViewModel(
            CreateRepositoryService(snapshot).Object,
            subscriptionService: subscriptions.Object);

        viewModel.RefreshPluginList();

        var removed = Assert.Single(
            viewModel.Plugins.Where(
                plugin => plugin.Id == "removed-plugin"));
        Assert.False(removed.IsRepositoryAvailable);
        Assert.Equal(Visibility.Visible, removed.RemovedTagVisibility);
        Assert.Equal(Visibility.Collapsed, removed.InstallButtonVisibility);
    }

    private static AvailablePluginsPageViewModel CreateViewModel(
        IPluginRepositoryService repositoryService,
        IPluginLibrary? pluginLibrary = null,
        IPluginSubscriptionService? subscriptionService = null,
        IPluginInstaller? installer = null,
        IPluginAcquisitionService? acquisitionService = null)
    {
        var library = pluginLibrary ?? CreatePluginLibrary().Object;
        var effectiveInstaller = installer ?? Mock.Of<IPluginInstaller>();
        var configService = new Mock<IConfigService>();
        configService.SetupGet(service => service.Config).Returns(new AppConfig());
        var subscriptions = subscriptionService ?? CreateSubscriptionService().Object;
        return new AvailablePluginsPageViewModel(
            library,
            Mock.Of<INotificationService>(),
            Mock.Of<IEventBus>(),
            repositoryService,
            subscriptions,
            effectiveInstaller,
            acquisitionService ??
            new PluginAcquisitionService(
                repositoryService,
                effectiveInstaller,
                library),
            configService.Object);
    }

    private static Mock<IPluginLibrary> CreatePluginLibrary()
    {
        var pluginLibrary = new Mock<IPluginLibrary>();
        pluginLibrary
            .Setup(library => library.GetInstalledPlugins())
            .Returns(new List<InstalledPluginInfo>());
        return pluginLibrary;
    }

    private static Mock<IPluginSubscriptionService> CreateSubscriptionService()
    {
        var subscriptions = new Mock<IPluginSubscriptionService>();
        subscriptions
            .Setup(service => service.Reconcile(
                It.IsAny<string>(),
                It.IsAny<PluginRepositorySnapshot>()))
            .Returns(Result.Success());
        subscriptions
            .Setup(service => service.GetSubscriptions())
            .Returns(Array.Empty<PluginSubscriptionRecord>());
        return subscriptions;
    }

    private static Mock<IPluginRepositoryService> CreateRepositoryService(
        PluginRepositorySnapshot snapshot)
    {
        var repositoryService = new Mock<IPluginRepositoryService>();
        repositoryService
            .SetupGet(service => service.RepositoryDirectory)
            .Returns("C:\\catalog-cache");
        repositoryService
            .SetupGet(service => service.Current)
            .Returns(snapshot);
        repositoryService
            .SetupGet(service => service.Settings)
            .Returns(new PluginRepositorySettings());
        repositoryService
            .Setup(service => service.SaveSettings(
                It.IsAny<PluginRepositorySettings>()))
            .Returns(Result.Success());
        repositoryService
            .Setup(service => service.InitializeAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<PluginRepositorySnapshot>.Success(snapshot));
        return repositoryService;
    }

    private static PluginRepositorySnapshot CreateSnapshot(bool usedCache)
    {
        return new PluginRepositorySnapshot(
            new PluginRepositoryIndex {
                SchemaVersion = 1,
                Commit = "0123456789abcdef0123456789abcdef01234567",
                Plugins = new List<PluginRepositoryEntry> {
                    new() {
                        Id = "alpha-plugin",
                        Path = "plugins/alpha-plugin",
                        Name = "Alpha",
                        Version = "2.0.0",
                        Description = "Alpha description",
                        DistributionType = AppConstants.PluginDistributionRepository,
                        MinHostVersion = "1.4.0"
                    },
                    new() {
                        Id = "beta-plugin",
                        Path = "plugins/beta-plugin",
                        Name = "Beta",
                        Version = "1.0.0",
                        Description = "Beta description",
                        DistributionType = AppConstants.PluginDistributionRelease,
                        HasBackend = true,
                        MinHostVersion = "1.4.0"
                    }
                }
            },
            CatalogCommit,
            usedCache);
    }
}
