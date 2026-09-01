using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Pages;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.ViewModels;

public sealed class ProfileMarketPageViewModelTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"AkashaNavigator.ProfileMarketViewModelTests.{Guid.NewGuid():N}");

    public ProfileMarketPageViewModelTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task InstallCommand_NewSubscription_SwitchesBeforePublishingProfileChange()
    {
        var calls = new List<string>();
        GameProfile? installedProfile = null;
        var profileManager = new Mock<IProfileManager>();
        profileManager
            .Setup(service => service.GetProfileById("genshin"))
            .Returns(() => installedProfile);
        profileManager
            .Setup(service => service.ImportProfile(
                It.Is<ProfileExportData>(data => data.ProfileId == "genshin"),
                false))
            .Callback(() => installedProfile = new GameProfile {
                Id = "genshin",
                Name = "原神",
                Version = 1
            })
            .Returns(ProfileImportResult.Success("genshin"));
        profileManager
            .Setup(service => service.SwitchProfile("genshin"))
            .Callback(() => calls.Add("switch"))
            .Returns(true);
        var eventBus = new Mock<IEventBus>();
        eventBus
            .Setup(service => service.Publish(It.IsAny<ProfileListChangedEvent>()))
            .Callback(() => calls.Add("publish"));
        var notifications = new Mock<INotificationService>();
        var marketplaceService = new ProfileMarketplaceService(
            Mock.Of<ILogService>(),
            profileManager.Object,
            Mock.Of<IPluginAssociationManager>(),
            Mock.Of<IPluginLibrary>(),
            Path.Combine(_tempDirectory, "marketplace-sources.json"));
        var viewModel = new ProfileMarketPageViewModel(
            marketplaceService,
            Mock.Of<IPluginLibrary>(),
            profileManager.Object,
            notifications.Object,
            eventBus.Object);
        var item = new MarketplaceProfileViewModel(
            new MarketplaceProfile {
                Id = "genshin",
                Name = "原神",
                Version = "1.0.0"
            },
            marketplaceService,
            profileManager.Object);
        var navigationRequestCount = 0;
        viewModel.NavigateToMyProfilesRequested += (_, _) => navigationRequestCount++;

        await viewModel.InstallCommand.ExecuteAsync(item);

        Assert.Equal(new[] { "switch", "publish" }, calls);
        Assert.Equal(1, navigationRequestCount);
        profileManager.Verify(
            service => service.SwitchProfile("genshin"),
            Times.Once);
        notifications.Verify(
            service => service.Success(
                It.Is<string>(message => message.Contains("已切换到 Profile \"原神\"")),
                "安装成功",
                3000),
            Times.Once);
    }

    [Fact]
    public async Task UpdateCommand_ExistingSubscription_DoesNotSwitchCurrentProfile()
    {
        var installedProfile = new GameProfile {
            Id = "genshin",
            Name = "原神",
            Version = 1
        };
        var profileManager = new Mock<IProfileManager>();
        profileManager
            .Setup(service => service.GetProfileById("genshin"))
            .Returns(installedProfile);
        profileManager
            .Setup(service => service.ImportProfile(
                It.IsAny<ProfileExportData>(),
                true))
            .Returns(ProfileImportResult.Success("genshin"));
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(service => service.ConfirmAsync(
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .ReturnsAsync(true);
        var marketplaceService = new ProfileMarketplaceService(
            Mock.Of<ILogService>(),
            profileManager.Object,
            Mock.Of<IPluginAssociationManager>(),
            Mock.Of<IPluginLibrary>(),
            Path.Combine(_tempDirectory, "marketplace-sources.json"));
        var viewModel = new ProfileMarketPageViewModel(
            marketplaceService,
            Mock.Of<IPluginLibrary>(),
            profileManager.Object,
            notifications.Object,
            Mock.Of<IEventBus>());
        var item = new MarketplaceProfileViewModel(
            new MarketplaceProfile {
                Id = "genshin",
                Name = "原神",
                Version = "2.0.0"
            },
            marketplaceService,
            profileManager.Object);
        var navigationRequestCount = 0;
        viewModel.NavigateToMyProfilesRequested += (_, _) => navigationRequestCount++;

        await viewModel.UpdateCommand.ExecuteAsync(item);

        profileManager.Verify(
            service => service.SwitchProfile(It.IsAny<string>()),
            Times.Never);
        Assert.Equal(0, navigationRequestCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
