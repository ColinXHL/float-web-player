using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Services;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginAcquisitionServiceTests
{
    [Fact]
    public async Task EnsureInstalledAsync_ReturnsExistingPluginWithoutRepositoryAccess()
    {
        var repository = new Mock<IPluginRepositoryService>();
        var installer = new Mock<IPluginInstaller>();
        var library = new Mock<IPluginLibrary>();
        var installed = new InstalledPluginInfo { Id = "sample-plugin", Version = "1.0.0" };
        library.Setup(service => service.IsInstalled("sample-plugin")).Returns(true);
        library.Setup(service => service.GetInstalledPluginInfo("sample-plugin")).Returns(installed);
        var service = new PluginAcquisitionService(repository.Object, installer.Object, library.Object);

        var result = await service.EnsureInstalledAsync("sample-plugin");

        Assert.True(result.IsSuccess);
        Assert.Same(installed, result.Value);
        repository.Verify(
            value => value.InitializeAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        installer.Verify(
            value => value.InstallOrUpdateRepositoryPluginAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureInstalledAsync_InitializesRepositoryBeforeInstalling()
    {
        var calls = new List<string>();
        var repository = new Mock<IPluginRepositoryService>();
        repository
            .Setup(service => service.InitializeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("initialize"))
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Success(CreateSnapshot()));
        var installer = new Mock<IPluginInstaller>();
        installer
            .Setup(service => service.InstallOrUpdateRepositoryPluginAsync(
                "sample-plugin",
                null,
                It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("install"))
            .ReturnsAsync(Result<InstalledPluginInfo>.Success(
                new InstalledPluginInfo { Id = "sample-plugin", Version = "1.0.0" }));
        var library = new Mock<IPluginLibrary>();
        var service = new PluginAcquisitionService(repository.Object, installer.Object, library.Object);

        var result = await service.EnsureInstalledAsync("sample-plugin");

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "initialize", "install" }, calls);
    }

    [Fact]
    public async Task EnsureInstalledAsync_DoesNotInstallWhenRepositoryInitializationFails()
    {
        var repository = new Mock<IPluginRepositoryService>();
        var expectedError = Error.Network("CATALOG_UNAVAILABLE", "catalog unavailable");
        repository
            .Setup(service => service.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PluginRepositorySnapshot>.Failure(expectedError));
        var installer = new Mock<IPluginInstaller>();
        var service = new PluginAcquisitionService(
            repository.Object,
            installer.Object,
            Mock.Of<IPluginLibrary>());

        var result = await service.EnsureInstalledAsync("sample-plugin");

        Assert.True(result.IsFailure);
        Assert.Same(expectedError, result.Error);
        installer.Verify(
            value => value.InstallOrUpdateRepositoryPluginAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<AkashaNavigator.Models.Update.PluginDownloadProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PluginRepositorySnapshot CreateSnapshot()
    {
        return new PluginRepositorySnapshot(
            new PluginRepositoryIndex(),
            new string('a', 40),
            false);
    }
}
