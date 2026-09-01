namespace AkashaNavigator.Helpers
{
/// <summary>
/// 显示器快照信息
/// 封装 Win32 MONITORINFOEX 的关键数据，用于多显示器场景
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// Win32 HMONITOR handle for querying monitor-specific properties.
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// Effective DPI scale for this monitor.
    /// </summary>
    public double DpiScale { get; set; } = 1.0;

    /// <summary>
    /// 显示器完整区域（物理像素坐标，可能包含任务栏区域）
    /// 坐标可能在多显示器虚拟桌面中为负值
    /// </summary>
    public Win32Helper.RECT MonitorRect { get; set; }

    /// <summary>
    /// 显示器工作区域（物理像素坐标，排除了任务栏等系统保留区域）
    /// </summary>
    public Win32Helper.RECT WorkAreaRect { get; set; }

    /// <summary>
    /// 是否为主显示器
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// 显示器设备名称（来自 MONITORINFOEX.szDevice，如 "\\.\DISPLAY1"）
    /// 用于跨会话持久化显示器身份
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 获取工作区域的 WPF 逻辑坐标 Rect
    /// </summary>
    /// <param name="dpiScale">DPI 缩放比例</param>
    /// <returns>WPF 逻辑坐标的 Rect</returns>
    public System.Windows.Rect GetWorkAreaAsWpfRect(double dpiScale)
    {
        return new System.Windows.Rect(
            WorkAreaRect.Left / dpiScale,
            WorkAreaRect.Top / dpiScale,
            (WorkAreaRect.Right - WorkAreaRect.Left) / dpiScale,
            (WorkAreaRect.Bottom - WorkAreaRect.Top) / dpiScale);
    }

    /// <summary>
    /// 获取完整显示器区域的 WPF 逻辑坐标 Rect
    /// </summary>
    /// <param name="dpiScale">DPI 缩放比例</param>
    /// <returns>WPF 逻辑坐标的 Rect</returns>
    public System.Windows.Rect GetMonitorRectAsWpfRect(double dpiScale)
    {
        return new System.Windows.Rect(
            MonitorRect.Left / dpiScale,
            MonitorRect.Top / dpiScale,
            (MonitorRect.Right - MonitorRect.Left) / dpiScale,
            (MonitorRect.Bottom - MonitorRect.Top) / dpiScale);
    }
}
}
