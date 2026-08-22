using System;
using System.IO;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;

namespace AkashaNavigator.Services
{
/// <summary>
/// 窗口状态服务
/// 负责保存和加载窗口位置、大小、最后访问 URL 等
/// 支持多显示器：持久化显示器身份，恢复时优先还原到同一显示器
/// </summary>
public class WindowStateService : IWindowStateService
{
#region Fields

    private readonly ILogService _logService;
    private readonly IProfileManager _profileManager;
    private readonly IMonitorLayoutService _monitorLayoutService;
    private WindowState? _cachedState;

#endregion

#region Constructor

    /// <summary>
    /// DI 容器使用的构造函数
    /// </summary>
    public WindowStateService(ILogService logService, IProfileManager profileManager,
                              IMonitorLayoutService monitorLayoutService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _monitorLayoutService = monitorLayoutService ?? throw new ArgumentNullException(nameof(monitorLayoutService));
    }

#endregion

#region Public Methods

    /// <summary>
    /// 加载窗口状态
    /// 如果保存的显示器不可用，自动将窗口位置调整到可用显示器
    /// </summary>
    public WindowState Load()
    {
        if (_cachedState != null)
            return _cachedState;

        var filePath = GetFilePath();
        var result = JsonHelper.LoadFromFile<WindowState>(filePath);

        if (result.IsSuccess)
        {
            _cachedState = result.Value;
        }
        else
        {
            _logService.Warn(nameof(WindowStateService), "加载窗口状态失败 [{FilePath}]: {ErrorMessage}", filePath,
                             result.Error?.Message ?? "未知错误");
            _cachedState = null;
        }

        // 返回默认状态
        if (_cachedState == null)
        {
            _cachedState = CreateDefaultState();
        }

        return _cachedState!;
    }

    /// <summary>
    /// 保存窗口状态
    /// </summary>
    public void Save(WindowState state)
    {
        _cachedState = state;

        var filePath = GetFilePath();
        var result = JsonHelper.SaveToFile(filePath, state);

        if (result.IsFailure)
        {
            _logService.Debug(nameof(WindowStateService), "保存窗口状态失败 [{FilePath}]: {ErrorMessage}", filePath,
                              result.Error?.Message ?? "未知错误");
        }
    }

    /// <summary>
    /// 更新并保存窗口状态
    /// </summary>
    public void Update(Action<WindowState> updateAction)
    {
        var state = Load();
        updateAction(state);
        Save(state);
    }

    /// <summary>
    /// 清除缓存，强制下次 Load 时重新从文件读取
    /// </summary>
    public void ClearCache()
    {
        _cachedState = null;
    }

#endregion

#region Private Methods

    private string GetFilePath()
    {
        return Path.Combine(_profileManager.GetCurrentProfileDirectory(), AppConstants.WindowStateFileName);
    }

    private WindowState CreateDefaultState()
    {
        // 使用 MonitorLayoutService 获取主显示器工作区
        var primaryMonitor = _monitorLayoutService.GetPrimaryMonitor();
        var dpiScale = double.IsFinite(primaryMonitor.DpiScale) && primaryMonitor.DpiScale > 0
            ? primaryMonitor.DpiScale
            : 1.0;
        var workAreaWpf = primaryMonitor.GetWorkAreaAsWpfRect(dpiScale);

        // 计算默认大小：宽度为工作区宽度的 1/4，高度按 16:9 比例计算
        double defaultWidth = Math.Max(workAreaWpf.Width / 4, AppConstants.MinWindowWidth);
        double defaultHeight = defaultWidth * 9 / 16;

        if (defaultHeight < AppConstants.MinWindowHeight)
        {
            defaultHeight = AppConstants.MinWindowHeight;
            defaultWidth = defaultHeight * 16 / 9;
        }

        // 定位到主显示器底部
        double left = workAreaWpf.Left;
        double top = workAreaWpf.Bottom - defaultHeight;

        return new WindowState { Left = left,
                                Top = top,
                                Width = defaultWidth,
                                Height = defaultHeight,
                                Opacity = AppConstants.MaxOpacity,
                                IsMaximized = false,
                                LastUrl = AppConstants.DefaultHomeUrl,
                                 IsMuted = false,
                                 MonitorDeviceName = primaryMonitor.DeviceName,
                                 PlayerWindowPlacementVersion = AppConstants.PlayerWindowPlacementVersion,
                                 PlayerWindowHorizontalAnchorRatio = 0.0,
                                 PlayerWindowVerticalAnchorRatio = 1.0,
                                 ControlBarCenterAnchorRatio = 0.5,
                                ControlBarPositionVersion = AppConstants.ControlBarPositionVersion,
                                ControlBarMonitorDeviceName = primaryMonitor.DeviceName };
    }

#endregion
}
}
