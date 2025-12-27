using System;
using System.Collections.Generic;
using System.Windows;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 覆盖层窗口管理服务
/// </summary>
public class OverlayManager : IOverlayManager
{
#region Singleton

    private static readonly Lazy<OverlayManager> _instance = new(() => new OverlayManager());

    public static OverlayManager Instance => _instance.Value;

    private OverlayManager()
    {
    }

#endregion

#region Fields

    private readonly Dictionary<string, OverlayWindow> _overlays = new();
    private readonly object _lock = new();

#endregion

#region Public Methods

    public OverlayWindow CreateOverlay(string pluginId, OverlayOptions? options = null)
    {
        lock (_lock)
        {
            // 如果已存在，先销毁旧的
            if (_overlays.TryGetValue(pluginId, out var existing))
            {
                existing.Close();
                _overlays.Remove(pluginId);
            }

            // 创建新的覆盖层窗口
            var overlay = new OverlayWindow(pluginId);

            // 应用选项
            if (options != null)
            {
                if (options.X.HasValue && options.Y.HasValue)
                {
                    overlay.SetPosition(options.X.Value, options.Y.Value);
                }

                if (options.Width.HasValue && options.Height.HasValue)
                {
                    overlay.SetSize(options.Width.Value, options.Height.Value);
                }
            }

            // 存储映射
            _overlays[pluginId] = overlay;

            return overlay;
        }
    }

    /// <summary>
    /// 获取插件的覆盖层窗口
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>覆盖层窗口，不存在则返回 null</returns>
    public OverlayWindow? GetOverlay(string pluginId)
    {
        lock (_lock)
        {
            return _overlays.TryGetValue(pluginId, out var overlay) ? overlay : null;
        }
    }

    /// <summary>
    /// 销毁插件的覆盖层窗口
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public void DestroyOverlay(string pluginId)
    {
        lock (_lock)
        {
            if (_overlays.TryGetValue(pluginId, out var overlay))
            {
                // 在 UI 线程上关闭窗口
                Application.Current?.Dispatcher.Invoke(() =>
                                                       {
                                                           overlay.ClearMarkers();
                                                           overlay.Hide();
                                                           overlay.Close();
                                                       });

                _overlays.Remove(pluginId);
            }
        }
    }

    /// <summary>
    /// 销毁所有覆盖层窗口
    /// </summary>
    public void DestroyAllOverlays()
    {
        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                                                       {
                                                           overlay.ClearMarkers();
                                                           overlay.Hide();
                                                           overlay.Close();
                                                       });
            }

            _overlays.Clear();
        }
    }

    /// <summary>
    /// 显示方向标记
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="direction">方向</param>
    /// <param name="durationMs">显示时长（毫秒），0 表示常驻</param>
    public void ShowDirectionMarker(string pluginId, Direction direction, int durationMs = 0)
    {
        var overlay = GetOverlay(pluginId);
        if (overlay != null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
                                                   { overlay.ShowDirectionMarker(direction, durationMs); });
        }
    }

    /// <summary>
    /// 清除插件的所有方向标记
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public void ClearMarkers(string pluginId)
    {
        var overlay = GetOverlay(pluginId);
        if (overlay != null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
                                                   { overlay.ClearMarkers(); });
        }
    }

#endregion
}

/// <summary>
/// 覆盖层选项
/// </summary>
public class OverlayOptions
{
    /// <summary>
    /// X 坐标（逻辑像素）
    /// </summary>
    public double? X { get; set; }

    /// <summary>
    /// Y 坐标（逻辑像素）
    /// </summary>
    public double? Y { get; set; }

    /// <summary>
    /// 宽度（逻辑像素）
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 高度（逻辑像素）
    /// </summary>
    public double? Height { get; set; }
}
}
