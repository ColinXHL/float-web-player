using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using Xunit;

namespace AkashaNavigator.Tests.Helpers;

public sealed class PlayerWindowPlacementCalculatorTests
{
    [Fact]
    public void Calculate_UsesTargetMonitorDpiAndRelativeAnchors()
    {
        var monitor = CreateMonitor("secondary", -2560, 0, 0, 1440, 1.5);
        var state = CreateCurrentState("secondary", 400, 300, 0.5, 0.5);

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(-1580, result.Bounds.Left);
        Assert.Equal(495, result.Bounds.Top);
        Assert.Equal(600, result.Bounds.Right - result.Bounds.Left);
        Assert.Equal(450, result.Bounds.Bottom - result.Bounds.Top);
        Assert.False(result.Recovered);
    }

    [Fact]
    public void Calculate_ClampsOversizedWindowBeforePosition()
    {
        var monitor = CreateMonitor("primary", 0, 0, 1280, 720, 1.5, isPrimary: true);
        var state = CreateCurrentState("primary", 4000, 3000, 1.0, 1.0);

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(0, result.Bounds.Left);
        Assert.Equal(0, result.Bounds.Top);
        Assert.Equal(1280, result.Bounds.Right);
        Assert.Equal(720, result.Bounds.Bottom);
    }

    [Fact]
    public void Calculate_UsesPrimaryMonitorWhenSavedMonitorWasRemoved()
    {
        var primary = CreateMonitor("primary", 0, 0, 1920, 1040, 1.0, isPrimary: true);
        var state = CreateCurrentState("removed", 640, 360, 0.25, 0.75);

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { primary }, 200, 150);

        Assert.Same(primary, result.Monitor);
        Assert.Equal(320, result.Bounds.Left);
        Assert.Equal(510, result.Bounds.Top);
        Assert.True(result.Recovered);
    }

    [Fact]
    public void Calculate_RecoversLegacyOffscreenCoordinates()
    {
        var monitor = CreateMonitor("primary", 0, 0, 1920, 1040, 1.0, isPrimary: true);
        var state = new WindowState {
            MonitorDeviceName = "primary",
            Left = 5000,
            Top = -2000,
            Width = 640,
            Height = 360
        };

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(1280, result.Bounds.Left);
        Assert.Equal(0, result.Bounds.Top);
        Assert.True(result.Recovered);
    }

    [Fact]
    public void Calculate_RecoversNonFiniteAndInvalidSize()
    {
        var monitor = CreateMonitor("primary", 0, 0, 1920, 1040, 1.25, isPrimary: true);
        var state = new WindowState {
            MonitorDeviceName = "primary",
            Left = double.NaN,
            Top = double.PositiveInfinity,
            Width = double.NaN,
            Height = -1
        };

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(250, result.Bounds.Right - result.Bounds.Left);
        Assert.Equal(188, result.Bounds.Bottom - result.Bounds.Top);
        Assert.True(result.Recovered);
    }

    [Fact]
    public void Calculate_RecoversExtremelyLargeFiniteCoordinates()
    {
        var monitor = CreateMonitor("primary", 0, 0, 1920, 1040, 1.0, isPrimary: true);
        var state = new WindowState {
            MonitorDeviceName = "primary",
            Left = double.MaxValue,
            Top = -double.MaxValue,
            Width = double.MaxValue,
            Height = double.MaxValue
        };

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(0, result.Bounds.Left);
        Assert.Equal(0, result.Bounds.Top);
        Assert.Equal(1920, result.Bounds.Right);
        Assert.Equal(1040, result.Bounds.Bottom);
    }

    [Fact]
    public void EnsureVisible_RequiresConfiguredVisibleAreaAtMonitorDpi()
    {
        var monitor = CreateMonitor("primary", 0, 0, 1920, 1080, 1.5, isPrimary: true);
        var mostlyOutside = new Win32Helper.RECT {
            Left = 1900,
            Top = 1060,
            Right = 2500,
            Bottom = 1510
        };

        var result = PlayerWindowPlacementCalculator.EnsureVisible(
            mostlyOutside,
            monitor,
            new[] { monitor },
            64);

        Assert.Equal(1320, result.Left);
        Assert.Equal(630, result.Top);
        Assert.Equal(1920, result.Right);
        Assert.Equal(1080, result.Bottom);
    }

    [Fact]
    public void Calculate_PreservesAnchorAfterPortraitRotation()
    {
        var portrait = CreateMonitor("secondary", 1920, 0, 3000, 1920, 1.0);
        var state = CreateCurrentState("secondary", 600, 400, 1.0, 1.0);

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { portrait }, 200, 150);

        Assert.Equal(2400, result.Bounds.Left);
        Assert.Equal(1520, result.Bounds.Top);
        Assert.Equal(3000, result.Bounds.Right);
        Assert.Equal(1920, result.Bounds.Bottom);
    }

    [Fact]
    public void CalculateAnchorRatios_RoundTripsPhysicalPlacement()
    {
        var monitor = CreateMonitor("secondary", -1920, -200, 0, 880, 1.25);
        var bounds = new Win32Helper.RECT {
            Left = -1200,
            Top = 100,
            Right = -600,
            Bottom = 550
        };
        var anchors = PlayerWindowPlacementCalculator.CalculateAnchorRatios(bounds, monitor);
        var state = CreateCurrentState(
            "secondary",
            (bounds.Right - bounds.Left) / monitor.DpiScale,
            (bounds.Bottom - bounds.Top) / monitor.DpiScale,
            anchors.Horizontal,
            anchors.Vertical);

        var result = PlayerWindowPlacementCalculator.Calculate(state, new[] { monitor }, 200, 150);

        Assert.Equal(bounds.Left, result.Bounds.Left);
        Assert.Equal(bounds.Top, result.Bounds.Top);
        Assert.Equal(bounds.Right, result.Bounds.Right);
        Assert.Equal(bounds.Bottom, result.Bounds.Bottom);
    }

    private static WindowState CreateCurrentState(
        string monitor,
        double width,
        double height,
        double horizontal,
        double vertical)
    {
        return new WindowState {
            MonitorDeviceName = monitor,
            Width = width,
            Height = height,
            PlayerWindowPlacementVersion = AppConstants.PlayerWindowPlacementVersion,
            PlayerWindowHorizontalAnchorRatio = horizontal,
            PlayerWindowVerticalAnchorRatio = vertical
        };
    }

    private static MonitorInfo CreateMonitor(
        string name,
        int left,
        int top,
        int right,
        int bottom,
        double dpi,
        bool isPrimary = false)
    {
        return new MonitorInfo {
            DeviceName = name,
            DpiScale = dpi,
            IsPrimary = isPrimary,
            MonitorRect = new Win32Helper.RECT { Left = left, Top = top, Right = right, Bottom = bottom },
            WorkAreaRect = new Win32Helper.RECT { Left = left, Top = top, Right = right, Bottom = bottom }
        };
    }
}
