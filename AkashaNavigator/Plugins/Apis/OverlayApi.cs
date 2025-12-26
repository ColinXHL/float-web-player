using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Plugins.Apis.Core;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Overlay API
/// </summary>
public class OverlayApi
{
    private readonly PluginContext _context;
    private readonly ConfigApi _configApi;

    public OverlayApi(PluginContext context, ConfigApi configApi)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configApi = configApi ?? throw new ArgumentNullException(nameof(configApi));
    }

    public void show()
    {
        try
        {
            var overlay = EnsureOverlay();
            System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.Show());
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void hide()
    {
        try
        {
            var overlay = OverlayManager.Instance.GetOverlay(_context.PluginId);
            System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.Hide());
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void setPosition(double x, double y)
    {
        try
        {
            var overlay = EnsureOverlay();
            System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.SetPosition((int)x, (int)y));
        }
        catch
        {
            // 在测试环境或无UI环境中忽略异常
        }
    }

    public void setSize(double width, double height)
    {
        // 忽略无效的尺寸
        if (width <= 0 || height <= 0)
            return;

        var overlay = EnsureOverlay();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.SetSize(width, height));
    }

    public void showMarker(string direction, int duration = 3000)
    {
        if (Enum.TryParse<Views.Windows.Direction>(direction, true, out var dir))
        {
            OverlayManager.Instance.ShowDirectionMarker(_context.PluginId, dir, duration);
        }
    }

    public void clearMarkers() => OverlayManager.Instance.ClearMarkers(_context.PluginId);

    /// <summary>
    /// 设置标记样式
    /// </summary>
    /// <param name="options">样式选项对象，包含 size 和/或 color 属性</param>
    public void setMarkerStyle(object? options)
    {
        if (options == null)
            return;

        var dict = JsTypeConverter.ToDictionary(options);
        if (dict == null || dict.Count == 0)
            return;

        double size = 24;
        string color = "#FFFF6B6B";

        if (dict.TryGetValue("size", out var sizeValue) && sizeValue != null)
            size = Convert.ToDouble(sizeValue);
        if (dict.TryGetValue("color", out var colorValue) && colorValue != null)
            color = colorValue.ToString() ?? "#FFFF6B6B";

        var overlay = EnsureOverlay();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.SetMarkerStyle(size, color));
    }

    /// <summary>
    /// 设置标记图片
    /// </summary>
    /// <param name="path">图片路径（相对于插件目录或绝对路径），图片应指向右/东方向</param>
    /// <returns>是否设置成功</returns>
    public bool setMarkerImage(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // 解析路径：如果是相对路径，则相对于插件目录
        string fullPath = path;
        if (!Path.IsPathRooted(path))
            fullPath = Path.Combine(_context.PluginDirectory, path);

        if (!File.Exists(fullPath))
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", "setMarkerImage: Image file not found: {FullPath}",
                                      fullPath);
            return false;
        }

        var overlay = EnsureOverlay();
        bool result = false;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  if (overlay != null)
                                                                      result = overlay.SetMarkerImage(fullPath);
                                                              });

        return result;
    }

    /// <summary>
    /// 确保覆盖层已创建
    /// </summary>
    private OverlayWindow? EnsureOverlay()
    {
        var overlay = OverlayManager.Instance.GetOverlay(_context.PluginId);
        if (overlay == null)
        {
            // 从配置读取初始位置和大小
            object? x = _configApi.Get("overlay.x", (object)50);
            object? y = _configApi.Get("overlay.y", (object)50);
            object? size = _configApi.Get("overlay.size", (object)200);

            var options = new OverlayOptions { X = Convert.ToDouble(x), Y = Convert.ToDouble(y),
                                               Width = Convert.ToDouble(size), Height = Convert.ToDouble(size) };

            overlay = OverlayManager.Instance.CreateOverlay(_context.PluginId, options);
        }
        return overlay;
    }

    /// <summary>
    /// 获取绘图上下文
    /// </summary>
    /// <returns>绘图上下文对象</returns>
    public OverlayContext getContext()
    {
        return new OverlayContext(_context.PluginId);
    }

    // 动画帧管理
    private static int _nextAnimationFrameId = 1;
    private readonly Dictionary<int, System.Windows.Threading.DispatcherTimer> _animationTimers = new();

    /// <summary>
    /// 请求动画帧（类似浏览器的 requestAnimationFrame）
    /// </summary>
    /// <param name="callback">回调函数，接收时间戳参数</param>
    /// <returns>动画帧 ID</returns>
    public int requestAnimationFrame(object callback)
    {
        var id = _nextAnimationFrameId++;
        var startTime = DateTime.Now;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60fps
                };

                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _animationTimers.Remove(id);

                    try
                    {
                        var timestamp = (DateTime.Now - startTime).TotalMilliseconds;
                        if (callback is ScriptObject scriptCallback)
                        {
                            scriptCallback.Invoke(false, timestamp);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Error($"Plugin:{_context.PluginId}",
                                                  "Animation frame callback error: {ErrorMessage}", ex.Message);
                    }
                };

                _animationTimers[id] = timer;
                timer.Start();
            });

        return id;
    }

    /// <summary>
    /// 取消动画帧
    /// </summary>
    /// <param name="id">动画帧 ID</param>
    public void cancelAnimationFrame(int id)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  if (_animationTimers.TryGetValue(id, out var timer))
                                                                  {
                                                                      timer.Stop();
                                                                      _animationTimers.Remove(id);
                                                                  }
                                                              });
    }
}
}
