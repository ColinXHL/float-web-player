using AkashaNavigator.Models.Config;

namespace AkashaNavigator.Helpers;

internal sealed record PlayerWindowPlacement(
    Win32Helper.RECT Bounds,
    MonitorInfo Monitor,
    bool Recovered);

/// <summary>
/// Calculates player bounds entirely in Win32 physical pixels.
/// </summary>
internal static class PlayerWindowPlacementCalculator
{
    public static PlayerWindowPlacement Calculate(
        WindowState state,
        IReadOnlyList<MonitorInfo> monitors,
        double minimumWidthDip,
        double minimumHeightDip)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }

        var savedMonitor = monitors.FirstOrDefault(
            monitor => string.Equals(
                monitor.DeviceName,
                state.MonitorDeviceName,
                StringComparison.OrdinalIgnoreCase));
        var target = savedMonitor ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
        var workArea = target.WorkAreaRect;
        var dpiScale = NormalizeDpi(target.DpiScale);
        var workWidth = Math.Max(1, workArea.Right - workArea.Left);
        var workHeight = Math.Max(1, workArea.Bottom - workArea.Top);

        var widthDip = IsFinitePositive(state.Width) ? state.Width : minimumWidthDip;
        var heightDip = IsFinitePositive(state.Height) ? state.Height : minimumHeightDip;
        var minimumWidthPx = Math.Max(1, Round(minimumWidthDip * dpiScale));
        var minimumHeightPx = Math.Max(1, Round(minimumHeightDip * dpiScale));
        var width = Math.Clamp(Round(widthDip * dpiScale), Math.Min(minimumWidthPx, workWidth), workWidth);
        var height = Math.Clamp(Round(heightDip * dpiScale), Math.Min(minimumHeightPx, workHeight), workHeight);

        var hasCurrentPlacement =
            state.PlayerWindowPlacementVersion >= AppConstants.PlayerWindowPlacementVersion &&
            double.IsFinite(state.PlayerWindowHorizontalAnchorRatio) &&
            double.IsFinite(state.PlayerWindowVerticalAnchorRatio);

        int left;
        int top;
        var recovered = savedMonitor == null || !hasCurrentPlacement;
        if (hasCurrentPlacement)
        {
            var horizontalRatio = Math.Clamp(state.PlayerWindowHorizontalAnchorRatio, 0.0, 1.0);
            var verticalRatio = Math.Clamp(state.PlayerWindowVerticalAnchorRatio, 0.0, 1.0);
            left = workArea.Left + Round((workWidth - width) * horizontalRatio);
            top = workArea.Top + Round((workHeight - height) * verticalRatio);
            recovered |= horizontalRatio != state.PlayerWindowHorizontalAnchorRatio ||
                         verticalRatio != state.PlayerWindowVerticalAnchorRatio;
        }
        else if (savedMonitor != null && double.IsFinite(state.Left) && double.IsFinite(state.Top))
        {
            left = Round(state.Left * dpiScale);
            top = Round(state.Top * dpiScale);
        }
        else
        {
            left = workArea.Left + ((workWidth - width) / 2);
            top = workArea.Top + ((workHeight - height) / 2);
        }

        var clampedLeft = Math.Clamp(left, workArea.Left, workArea.Right - width);
        var clampedTop = Math.Clamp(top, workArea.Top, workArea.Bottom - height);
        var clamped = CreateRect(clampedLeft, clampedTop, width, height);
        recovered |= left != clampedLeft || top != clampedTop ||
                     !IsFinitePositive(state.Width) ||
                     !IsFinitePositive(state.Height);
        return new PlayerWindowPlacement(clamped, target, recovered);
    }

    public static Win32Helper.RECT EnsureVisible(
        Win32Helper.RECT bounds,
        MonitorInfo target,
        IReadOnlyList<MonitorInfo> monitors,
        double minimumVisibleDip)
    {
        var targetWidth = Math.Max(1, target.WorkAreaRect.Right - target.WorkAreaRect.Left);
        var targetHeight = Math.Max(1, target.WorkAreaRect.Bottom - target.WorkAreaRect.Top);
        var width = (long)bounds.Right - bounds.Left;
        var height = (long)bounds.Bottom - bounds.Top;
        if (width <= targetWidth && height <= targetHeight &&
            HasMinimumVisibleArea(bounds, monitors, minimumVisibleDip))
        {
            return bounds;
        }

        return ClampToMonitor(bounds, target);
    }

    public static bool HasMinimumVisibleArea(
        Win32Helper.RECT bounds,
        IReadOnlyList<MonitorInfo> monitors,
        double minimumVisibleDip)
    {
        foreach (var monitor in monitors)
        {
            var minimumVisiblePx = Math.Max(1, Round(minimumVisibleDip * NormalizeDpi(monitor.DpiScale)));
            var intersectionWidth = Math.Max(
                0L,
                (long)Math.Min(bounds.Right, monitor.WorkAreaRect.Right) -
                Math.Max(bounds.Left, monitor.WorkAreaRect.Left));
            var intersectionHeight = Math.Max(
                0L,
                (long)Math.Min(bounds.Bottom, monitor.WorkAreaRect.Bottom) -
                Math.Max(bounds.Top, monitor.WorkAreaRect.Top));
            if (intersectionWidth >= minimumVisiblePx && intersectionHeight >= minimumVisiblePx)
            {
                return true;
            }
        }

        return false;
    }

    public static Win32Helper.RECT ClampToMonitor(Win32Helper.RECT bounds, MonitorInfo monitor)
    {
        var workArea = monitor.WorkAreaRect;
        var workWidth = Math.Max(1, workArea.Right - workArea.Left);
        var workHeight = Math.Max(1, workArea.Bottom - workArea.Top);
        var width = (int)Math.Clamp((long)bounds.Right - bounds.Left, 1L, workWidth);
        var height = (int)Math.Clamp((long)bounds.Bottom - bounds.Top, 1L, workHeight);
        var left = Math.Clamp(bounds.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(bounds.Top, workArea.Top, workArea.Bottom - height);
        return CreateRect(left, top, width, height);
    }

    public static (double Horizontal, double Vertical) CalculateAnchorRatios(
        Win32Helper.RECT bounds,
        MonitorInfo monitor)
    {
        var clamped = ClampToMonitor(bounds, monitor);
        var workArea = monitor.WorkAreaRect;
        var horizontalRange = Math.Max(0, (workArea.Right - workArea.Left) - (clamped.Right - clamped.Left));
        var verticalRange = Math.Max(0, (workArea.Bottom - workArea.Top) - (clamped.Bottom - clamped.Top));
        var horizontal = horizontalRange == 0
            ? 0.5
            : (double)(clamped.Left - workArea.Left) / horizontalRange;
        var vertical = verticalRange == 0
            ? 0.5
            : (double)(clamped.Top - workArea.Top) / verticalRange;
        return (Math.Clamp(horizontal, 0.0, 1.0), Math.Clamp(vertical, 0.0, 1.0));
    }

    private static Win32Helper.RECT CreateRect(int left, int top, int width, int height)
    {
        return new Win32Helper.RECT
        {
            Left = left,
            Top = top,
            Right = ClampToInt((long)left + width),
            Bottom = ClampToInt((long)top + height)
        };
    }

    private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

    private static double NormalizeDpi(double dpiScale) =>
        double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1.0;

    private static int Round(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (rounded <= int.MinValue)
        {
            return int.MinValue;
        }

        return (int)rounded;
    }

    private static int ClampToInt(long value) =>
        (int)Math.Clamp(value, int.MinValue, int.MaxValue);
}
