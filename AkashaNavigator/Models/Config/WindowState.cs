namespace AkashaNavigator.Models.Config
{
/// <summary>
/// 窗口状态模型
/// 用于 JSON 序列化保存窗口位置和大小（Phase 15）
/// </summary>
public class WindowState
{
    /// <summary>
    /// 窗口左边位置
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// 窗口顶部位置
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 窗口透明度
    /// </summary>
    public double Opacity { get; set; } = AppConstants.MaxOpacity;

    /// <summary>
    /// 是否最大化
    /// </summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// 最后访问的 URL
    /// </summary>
    public string? LastUrl { get; set; }

    /// <summary>
    /// 是否静音
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// 窗口所在显示器的设备名称（如 "\\.\DISPLAY1"）
    /// 用于跨会话持久化显示器身份，确保恢复到同一显示器
    /// 为空时表示没有保存显示器信息（向后兼容旧配置）
    /// </summary>
    public string? MonitorDeviceName { get; set; }

    /// <summary>
    /// 播放器窗口位置算法版本。0 表示旧版全局 WPF 坐标。
    /// </summary>
    public int PlayerWindowPlacementVersion { get; set; }

    /// <summary>
    /// 窗口左边在目标显示器可移动横向范围中的比例（0-1）。
    /// </summary>
    public double PlayerWindowHorizontalAnchorRatio { get; set; } = 0.5;

    /// <summary>
    /// 窗口顶部在目标显示器可移动纵向范围中的比例（0-1）。
    /// </summary>
    public double PlayerWindowVerticalAnchorRatio { get; set; } = 0.5;

    /// <summary>
    /// 控制栏中心点在显示器工作区中的横向比例（0-1）
    /// 0.5 表示居中；用于跨显示器和跨会话恢复控制栏位置
    /// </summary>
    public double ControlBarCenterAnchorRatio { get; set; } = 0.5;

    /// <summary>
    /// 控制栏位置算法版本。
    /// 低版本配置可能包含旧坐标换算造成的偏移锚点。
    /// </summary>
    public int ControlBarPositionVersion { get; set; }

    /// <summary>
    /// 控制栏上次所在显示器的设备名称（如 "\\.\DISPLAY1"）
    /// 用于跨会话优先恢复到同一显示器
    /// </summary>
    public string? ControlBarMonitorDeviceName { get; set; }
}
}
