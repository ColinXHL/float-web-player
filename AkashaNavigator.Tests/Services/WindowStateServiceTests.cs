using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;
using System.IO;
using Moq;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class WindowStateServiceTests
{
    [Fact]
    public void Load_CreatesHighDpiDefaultStateInLogicalUnits()
    {
        var profiles = new Mock<IProfileManager>();
        profiles
            .Setup(service => service.GetCurrentProfileDirectory())
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var monitors = new Mock<IMonitorLayoutService>();
        monitors.Setup(service => service.GetPrimaryMonitor()).Returns(
            new MonitorInfo {
                DeviceName = "primary",
                IsPrimary = true,
                DpiScale = 1.5,
                MonitorRect = new Win32Helper.RECT {
                    Left = 0,
                    Top = 0,
                    Right = 2560,
                    Bottom = 1440
                },
                WorkAreaRect = new Win32Helper.RECT {
                    Left = 0,
                    Top = 0,
                    Right = 2560,
                    Bottom = 1400
                }
            });
        var service = new WindowStateService(
            Mock.Of<ILogService>(),
            profiles.Object,
            monitors.Object);

        var state = service.Load();

        Assert.Equal(2560.0 / 1.5 / 4.0, state.Width, 6);
        Assert.Equal(state.Width * 9.0 / 16.0, state.Height, 6);
        Assert.Equal((1400.0 / 1.5) - state.Height, state.Top, 6);
        Assert.Equal(AppConstants.PlayerWindowPlacementVersion, state.PlayerWindowPlacementVersion);
        Assert.Equal(0.0, state.PlayerWindowHorizontalAnchorRatio);
        Assert.Equal(1.0, state.PlayerWindowVerticalAnchorRatio);
    }
}
