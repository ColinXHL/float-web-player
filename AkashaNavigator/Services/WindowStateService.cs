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
            // 校验窗口位置是否在可见工作区内，越界自动拉回
            EnsureWindowVisible(_cachedState);
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

    /// <summary>
    /// 校验窗口位置是否与可见工作区有交集，完全越界时拉回工作区左上角。
    /// 防止显示器分辨率变化/配置损坏导致窗口持久化到屏幕外（用户无法找回，
    /// 因为播放器窗口带 WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW，点击/Alt+Tab 均不可达）。
    /// </summary>
    private void EnsureWindowVisible(WindowState state)
    {
        var monitor = !string.IsNullOrEmpty(state.MonitorDeviceName)
            ? _monitorLayoutService.FindMonitorByDeviceName(state.MonitorDeviceName)
            : null;
        monitor ??= _monitorLayoutService.GetPrimaryMonitor();

        var workArea = monitor.GetWorkAreaAsWpfRect(1.0);

        // 窗口与工作区有可见交集才算可见（完全在屏幕外时无交集）
        bool visible = state.Left < workArea.Right &&
                       state.Top < workArea.Bottom &&
                       state.Left + state.Width > workArea.Left &&
                       state.Top + state.Height > workArea.Top;

        if (!visible)
        {
            _logService.Warn(nameof(WindowStateService),
                "窗口位置越界（Left={Left}, Top={Top}），已重置到工作区左上角", state.Left, state.Top);
            state.Left = workArea.Left;
            state.Top = workArea.Top;
        }
    }

    private WindowState CreateDefaultState()
    {
        // 使用 MonitorLayoutService 获取主显示器工作区
        var primaryMonitor = _monitorLayoutService.GetPrimaryMonitor();
        var workAreaWpf = primaryMonitor.GetWorkAreaAsWpfRect(1.0);
        var monitorRectWpf = primaryMonitor.GetMonitorRectAsWpfRect(1.0);

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
        double top = monitorRectWpf.Bottom - defaultHeight;

        return new WindowState { Left = left,
                                Top = top,
                                Width = defaultWidth,
                                Height = defaultHeight,
                                Opacity = AppConstants.MaxOpacity,
                                IsMaximized = false,
                                LastUrl = AppConstants.DefaultHomeUrl,
                                IsMuted = false,
                                MonitorDeviceName = primaryMonitor.DeviceName,
                                ControlBarCenterAnchorRatio = 0.5,
                                ControlBarPositionVersion = AppConstants.ControlBarPositionVersion,
                                ControlBarMonitorDeviceName = primaryMonitor.DeviceName };
    }

#endregion
}
}
