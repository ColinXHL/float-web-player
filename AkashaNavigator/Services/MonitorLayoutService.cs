using System;
using System.Windows;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using Microsoft.Win32;

namespace AkashaNavigator.Services
{
/// <inheritdoc/>
public class MonitorLayoutService : IMonitorLayoutService, IDisposable
{
    private readonly ILogService _logService;
    private readonly IEventBus _eventBus;
    private bool _disposed;

    public MonitorLayoutService(ILogService logService, IEventBus eventBus)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        // 订阅系统显示设置变化事件
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <inheritdoc/>
    public MonitorInfo? GetMonitorFromWindow(IntPtr hwnd)
    {
        return Win32Helper.GetMonitorInfoFromWindow(hwnd);
    }

    /// <inheritdoc/>
    public MonitorInfo GetMonitorFromWindowOrDefault(IntPtr hwnd)
    {
        var monitor = GetMonitorFromWindow(hwnd);
        if (monitor != null)
            return monitor;

        // 回退到主显示器
        _logService.Warn(nameof(MonitorLayoutService), "无法获取窗口所在显示器，回退到主显示器");
        return GetPrimaryMonitor();
    }

    /// <inheritdoc/>
    public MonitorInfo? FindMonitorByDeviceName(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
            return null;

        // 枚举所有显示器查找匹配的设备名称
        var monitors = Win32Helper.EnumerateMonitors();
        foreach (var monitor in monitors)
        {
            if (string.Equals(monitor.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                return monitor;
        }

        return null;
    }

    /// <inheritdoc/>
    public MonitorInfo GetPrimaryMonitor()
    {
        // 优先通过 Win32 API 获取真实主显示器信息
        var monitors = Win32Helper.EnumerateMonitors();
        foreach (var monitor in monitors)
        {
            if (monitor.IsPrimary)
                return monitor;
        }

        // 如果 Win32 枚举失败，回退到 SystemParameters
        // SystemParameters.WorkArea 在 WPF 中始终返回主显示器工作区
        var workArea = SystemParameters.WorkArea;
        var primaryScreenHeight = SystemParameters.PrimaryScreenHeight;
        var primaryScreenWidth = SystemParameters.PrimaryScreenWidth;

        // 计算物理像素坐标（使用主显示器的 DPI 缩放）
        double dpiScale = 1.0;
        var app = Application.Current;
        if (app?.MainWindow != null)
        {
            var source = PresentationSource.FromVisual(app.MainWindow);
            dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        }

        return new MonitorInfo
        {
            DpiScale = dpiScale,
            MonitorRect = new Win32Helper.RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)(primaryScreenWidth * dpiScale),
                Bottom = (int)(primaryScreenHeight * dpiScale)
            },
            WorkAreaRect = Win32Helper.ToPhysicalRect(workArea, dpiScale),
            IsPrimary = true,
            DeviceName = "Primary"
        };
    }

    /// <inheritdoc/>
    public void Refresh()
    {
        // 缓存由 Win32Helper 管理，此处仅记录日志
        _logService.Info(nameof(MonitorLayoutService), "显示器拓扑缓存刷新请求");
    }

    /// <summary>
    /// 系统显示设置变化回调
    /// </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _logService.Info(nameof(MonitorLayoutService), "检测到系统显示设置变化");

        // 发布拓扑变化事件
        _eventBus.Publish(new DisplayTopologyChangedEvent());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _disposed = true;
        }
    }
}
}
