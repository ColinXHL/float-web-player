using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using System.Windows;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class MyProfilesPageViewModelTests
{
    [Fact]
    public async Task InstallMissingCommand_AcquiresBeforeRestoringAssociation()
    {
        var calls = new List<string>();
        var acquisition = new Mock<IPluginAcquisitionService>();
        acquisition
            .Setup(service => service.EnsureInstalledAsync(
                "sample-plugin",
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("acquire"))
            .ReturnsAsync(Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo { Id = "sample-plugin", Version = "1.0.0" }));
        var viewModel = CreateViewModel(
            acquisition,
            out var associations,
            out var library,
            out var pluginHost,
            out var eventBus,
            out _);
        associations
            .Setup(service => service.GetMissingOriginalPlugins("profile"))
            .Returns(new List<string> { "sample-plugin" });
        associations
            .Setup(service => service.AddPluginToProfile("sample-plugin", "profile", true))
            .Callback(() => calls.Add("associate"))
            .Returns(true);
        viewModel.CurrentProfileId = "profile";

        await viewModel.InstallMissingCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "acquire", "associate" }, calls);
        library.Verify(
            service => service.InstallPlugin(It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
        pluginHost.Verify(service => service.LoadPluginsForProfile("profile"), Times.Once);
        eventBus.Verify(service => service.Publish(It.IsAny<PluginListChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task InstallMissingCommand_DoesNotRestoreAssociationWhenAcquisitionFails()
    {
        var acquisition = new Mock<IPluginAcquisitionService>();
        acquisition
            .Setup(service => service.EnsureInstalledAsync(
                "missing-plugin",
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.RepositoryPluginNotFound,
                    "插件仓库中不存在 missing-plugin",
                    pluginId: "missing-plugin")));
        var viewModel = CreateViewModel(
            acquisition,
            out var associations,
            out _,
            out var pluginHost,
            out _,
            out var notifications);
        associations
            .Setup(service => service.GetMissingOriginalPlugins("profile"))
            .Returns(new List<string> { "missing-plugin" });
        viewModel.CurrentProfileId = "profile";

        await viewModel.InstallMissingCommand.ExecuteAsync(null);

        associations.Verify(
            service => service.AddPluginToProfile(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()),
            Times.Never);
        pluginHost.Verify(
            service => service.LoadPluginsForProfile(It.IsAny<string>()),
            Times.Never);
        notifications.Verify(
            service => service.Warning(
                It.Is<string>(message => message.Contains("插件仓库中不存在 missing-plugin")),
                null,
                3000),
            Times.Once);
    }

    [Fact]
    public async Task InstallPluginCommand_RefreshesCurrentProfileRuntime()
    {
        var acquisition = new Mock<IPluginAcquisitionService>();
        acquisition
            .Setup(service => service.EnsureInstalledAsync(
                "sample-plugin",
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo { Id = "sample-plugin", Version = "1.0.0" }));
        var viewModel = CreateViewModel(
            acquisition,
            out _,
            out _,
            out var pluginHost,
            out var eventBus,
            out _);
        viewModel.CurrentProfileId = "profile";

        await viewModel.InstallPluginCommand.ExecuteAsync("sample-plugin");

        pluginHost.Verify(service => service.LoadPluginsForProfile("profile"), Times.Once);
        eventBus.Verify(service => service.Publish(It.IsAny<PluginListChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task InstallMissingCommand_ShowsProgressAndDisablesInstallCommands()
    {
        var completion = new TaskCompletionSource<Result<InstalledPluginInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var acquisition = new Mock<IPluginAcquisitionService>();
        acquisition
            .Setup(service => service.EnsureInstalledAsync(
                "sample-plugin",
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(completion.Task);
        var viewModel = CreateViewModel(
            acquisition,
            out var associations,
            out _,
            out _,
            out _,
            out _);
        associations
            .Setup(service => service.GetMissingPlugins("profile"))
            .Returns(new List<string> { "sample-plugin" });
        viewModel.CurrentProfileId = "profile";

        var operation = viewModel.InstallMissingCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPluginInstallBusy);
        Assert.True(viewModel.IsPluginInstallProgressIndeterminate);
        Assert.Contains("sample-plugin", viewModel.PluginInstallStatusText);
        Assert.False(viewModel.InstallMissingCommand.CanExecute(null));
        Assert.False(viewModel.InstallPluginCommand.CanExecute("sample-plugin"));

        completion.SetResult(
            Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo { Id = "sample-plugin", Version = "1.0.0" }));
        await operation;

        Assert.False(viewModel.IsPluginInstallBusy);
        Assert.True(viewModel.InstallPluginCommand.CanExecute("sample-plugin"));
    }

    [Fact]
    public void RefreshPluginList_UsesCatalogMetadataForMissingPlugin()
    {
        var acquisition = new Mock<IPluginAcquisitionService>();
        var repository = new Mock<IPluginRepositoryService>();
        repository.SetupGet(service => service.Current).Returns(
            new PluginRepositorySnapshot(
                new PluginRepositoryIndex {
                    Plugins = new List<PluginRepositoryEntry> {
                        new() {
                            Id = "sample-plugin",
                            Name = "Catalog Name",
                            Version = "2.0.0",
                            Description = "Catalog description"
                        }
                    }
                },
                new string('a', 40),
                true));
        var viewModel = CreateViewModel(
            acquisition,
            out var associations,
            out _,
            out _,
            out _,
            out _,
            repository.Object);
        associations
            .Setup(service => service.GetPluginsInProfile("profile"))
            .Returns(new List<PluginReference> {
                new() {
                    PluginId = "sample-plugin",
                    Status = PluginInstallStatus.Missing
                }
            });
        associations
            .Setup(service => service.GetMissingPlugins("profile"))
            .Returns(new List<string> { "sample-plugin" });
        viewModel.CurrentProfileId = "profile";

        viewModel.RefreshPluginList();

        var plugin = Assert.Single(viewModel.Plugins);
        Assert.Equal("Catalog Name", plugin.Name);
        Assert.Equal("2.0.0", plugin.Version);
        Assert.Equal("Catalog description", plugin.Description);
    }

    [Fact]
    public void ProfileChanged_SelectsTheNewRuntimeProfileInThePage()
    {
        var defaultProfile = new GameProfile { Id = "default", Name = "默认" };
        var subscribedProfile = new GameProfile { Id = "genshin", Name = "原神" };
        var currentProfile = defaultProfile;
        var profileManager = new Mock<IProfileManager>();
        profileManager
            .SetupGet(service => service.CurrentProfile)
            .Returns(() => currentProfile);
        profileManager
            .SetupGet(service => service.Profiles)
            .Returns(new List<GameProfile> { defaultProfile, subscribedProfile });
        var viewModel = CreateViewModel(
            new Mock<IPluginAcquisitionService>(),
            out _,
            out _,
            out _,
            out _,
            out _,
            profileManager: profileManager.Object);
        viewModel.RefreshProfileList();
        Assert.Equal("default", viewModel.SelectedProfile?.Id);

        currentProfile = subscribedProfile;
        profileManager.Raise(
            service => service.ProfileChanged += null,
            profileManager.Object,
            subscribedProfile);

        Assert.Equal("genshin", viewModel.CurrentProfileId);
        Assert.Equal("genshin", viewModel.SelectedProfile?.Id);
        Assert.Equal(Visibility.Collapsed, viewModel.SetCurrentButtonVisibility);
    }

    private static MyProfilesPageViewModel CreateViewModel(
        Mock<IPluginAcquisitionService> acquisition,
        out Mock<IPluginAssociationManager> associations,
        out Mock<IPluginLibrary> library,
        out Mock<IPluginHost> pluginHost,
        out Mock<IEventBus> eventBus,
        out Mock<INotificationService> notifications,
        IPluginRepositoryService? repository = null,
        IProfileManager? profileManager = null)
    {
        var effectiveProfileManager = profileManager;
        if (effectiveProfileManager == null)
        {
            var defaultProfileManager = new Mock<IProfileManager>();
            defaultProfileManager.SetupGet(service => service.CurrentProfile).Returns(
                new GameProfile { Id = "profile", Name = "Profile" });
            defaultProfileManager.SetupGet(service => service.Profiles).Returns(
                new List<GameProfile> { new() { Id = "profile", Name = "Profile" } });
            effectiveProfileManager = defaultProfileManager.Object;
        }

        associations = new Mock<IPluginAssociationManager>();
        associations
            .Setup(service => service.GetPluginsInProfile(It.IsAny<string>()))
            .Returns(new List<PluginReference>());
        associations
            .Setup(service => service.GetMissingPlugins(It.IsAny<string>()))
            .Returns(new List<string>());
        associations
            .Setup(service => service.GetMissingOriginalPlugins(It.IsAny<string>()))
            .Returns(new List<string>());

        library = new Mock<IPluginLibrary>();
        pluginHost = new Mock<IPluginHost>();
        eventBus = new Mock<IEventBus>();
        notifications = new Mock<INotificationService>();

        return new MyProfilesPageViewModel(
            effectiveProfileManager,
            associations.Object,
            library.Object,
            pluginHost.Object,
            notifications.Object,
            eventBus.Object,
            Mock.Of<IProfileDeletionWorkflow>(),
            acquisition.Object,
            repository ?? Mock.Of<IPluginRepositoryService>());
    }
}
