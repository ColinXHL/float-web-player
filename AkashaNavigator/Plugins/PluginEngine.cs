using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AkashaNavigator.Models.Data;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 插件引擎初始化器
/// 负责配置 V8 引擎并暴露所有 API 和类型
/// </summary>
/// <remarks>
/// 设计说明：
/// - 采用扁平化结构，API 作为独立全局对象暴露
/// - 支持 ES6 模块导入
/// - 允许插件直接实例化 C# 类型
/// - C# Task 自动转换为 JavaScript Promise
/// </remarks>
public static class PluginEngine
{
#region Public Methods

    /// <summary>
    /// 初始化插件引擎，暴露所有 API 和类型
    /// </summary>
    /// <param name="engine">V8 脚本引擎实例</param>
    /// <param name="pluginDir">插件目录路径</param>
    /// <param name="configDir">配置目录路径（用于存储用户数据）</param>
    /// <param name="libraryPaths">ES6 模块搜索路径列表</param>
    /// <param name="config">插件配置</param>
    /// <param name="manifest">插件清单</param>
    /// <param name="options">初始化选项（可选）</param>
    public static void InitializeEngine(V8ScriptEngine engine, string pluginDir, string configDir,
                                        string[]? libraryPaths, PluginConfig config, PluginManifest manifest,
                                        PluginEngineOptions? options = null)
    {
        if (engine == null)
            throw new ArgumentNullException(nameof(engine));
        if (string.IsNullOrEmpty(pluginDir))
            throw new ArgumentException("Plugin directory cannot be empty", nameof(pluginDir));
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var pluginId = manifest.Id ?? "unknown";
        options ??= new PluginEngineOptions();

        LogService.Instance.Debug($"PluginEngine:{pluginId}", "Initializing engine...");

        // 1. 配置 ES6 模块加载
        ConfigureModuleLoading(engine, pluginDir, libraryPaths);

        // 2. 创建共享的 EventManager
        var eventManager = new EventManager();

        // 3. 暴露全局对象
        ExposeGlobalObjects(engine, pluginId, pluginDir, configDir, config, manifest, eventManager, options);

        // 4. 暴露全局便捷函数
        ExposeGlobalFunctions(engine);

        // 5. 暴露 C# 类型
        ExposeCSharpTypes(engine);

        LogService.Instance.Debug($"PluginEngine:{pluginId}", "Engine initialization complete");
    }

    /// <summary>
    /// 创建并初始化新的 V8 引擎实例
    /// </summary>
    /// <param name="pluginDir">插件目录路径</param>
    /// <param name="configDir">配置目录路径</param>
    /// <param name="libraryPaths">ES6 模块搜索路径列表</param>
    /// <param name="config">插件配置</param>
    /// <param name="manifest">插件清单</param>
    /// <param name="options">初始化选项（可选）</param>
    /// <returns>已初始化的 V8 引擎实例</returns>
    public static V8ScriptEngine CreateEngine(string pluginDir, string configDir, string[]? libraryPaths,
                                              PluginConfig config, PluginManifest manifest,
                                              PluginEngineOptions? options = null)
    {
        // 创建 V8 引擎，启用关键标志
        var engineFlags = V8ScriptEngineFlags.EnableDebugging | V8ScriptEngineFlags.UseCaseInsensitiveMemberBinding |
                          V8ScriptEngineFlags.EnableTaskPromiseConversion;

        var engine = new V8ScriptEngine(engineFlags);

        // 配置内存限制
        engine.MaxRuntimeHeapSize = (UIntPtr)50_000_000;     // 50MB
        engine.MaxRuntimeStackUsage = (UIntPtr)(100 * 1024); // 100KB stack

        // 初始化引擎
        InitializeEngine(engine, pluginDir, configDir, libraryPaths, config, manifest, options);

        return engine;
    }

#endregion

#region Private Methods - Module Loading

    /// <summary>
    /// 配置 ES6 模块加载
    /// </summary>
    private static void ConfigureModuleLoading(V8ScriptEngine engine, string pluginDir, string[]? libraryPaths)
    {
        // 启用文件加载和模块支持
        engine.DocumentSettings.AccessFlags =
            DocumentAccessFlags.EnableFileLoading | DocumentAccessFlags.AllowCategoryMismatch;

        // 构建搜索路径
        var searchPaths = new List<string> { pluginDir };

        if (libraryPaths != null)
        {
            foreach (var path in libraryPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                // 解析相对路径
                var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(pluginDir, path));

                if (Directory.Exists(fullPath) && !searchPaths.Contains(fullPath))
                {
                    searchPaths.Add(fullPath);
                }
            }
        }

        // 设置搜索路径
        engine.DocumentSettings.SearchPath = string.Join(";", searchPaths);

        LogService.Instance.Debug("PluginEngine", $"Module search paths: {engine.DocumentSettings.SearchPath}");
    }

#endregion

#region Private Methods - Global Objects

    /// <summary>
    /// 暴露全局对象
    /// </summary>
    private static void ExposeGlobalObjects(V8ScriptEngine engine, string pluginId, string pluginDir, string configDir,
                                            PluginConfig config, PluginManifest manifest, EventManager eventManager,
                                            PluginEngineOptions options)
    {
        // 创建临时的 PluginContext 用于 API 初始化
        // 注意：这里创建的是一个轻量级的上下文，仅用于 API 初始化
        var tempContext = new PluginContextLite(pluginId, pluginDir, configDir, manifest);

        // 1. 暴露 log 全局对象
        var logApi = new LogApi(pluginId);
        engine.AddHostObject("log", logApi);

        // 2. 暴露 config 全局对象
        var configApi = new ConfigApi(config, eventManager);
        engine.AddHostObject("config", configApi);

        // 3. 暴露 settings 全局对象（动态代理）
        var settingsProxy = new SettingsProxy(config, manifest.DefaultConfig, pluginId);
        engine.AddHostObject("settings", settingsProxy);

        // 4. 暴露 profile 全局对象
        var profileInfo = CreateProfileInfo(options);
        engine.AddHostObject("profile", profileInfo);

        // 5. 暴露 plugin 全局对象（插件元信息）
        var pluginInfo = new PluginInfoProxy(manifest);
        engine.AddHostObject("plugin", pluginInfo);

        // 6. 暴露 API 对象（根据权限）
        ExposeApiObjects(engine, tempContext, configApi, eventManager, manifest, options);

        LogService.Instance.Debug($"PluginEngine:{pluginId}", "Global objects exposed");
    }

    /// <summary>
    /// 暴露 API 对象
    /// </summary>
    private static void ExposeApiObjects(V8ScriptEngine engine, PluginContextLite context, ConfigApi configApi,
                                         EventManager eventManager, PluginManifest manifest,
                                         PluginEngineOptions options)
    {
        var permissions = manifest.Permissions ?? new List<string>();
        var pluginId = context.PluginId;

        // overlay API
        if (permissions.Contains(PluginPermissionsV2.Overlay, StringComparer.OrdinalIgnoreCase))
        {
            var overlayApi = new OverlayApiLite(context, configApi);
            engine.AddHostObject("overlay", overlayApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: overlay");
        }

        // player API
        if (permissions.Contains(PluginPermissionsV2.Player, StringComparer.OrdinalIgnoreCase))
        {
            var playerApi = new PlayerApiLite(context, options.GetPlayerWindow);
            playerApi.SetEventManager(eventManager);
            engine.AddHostObject("player", playerApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: player");
        }

        // window API
        if (permissions.Contains(PluginPermissionsV2.Window, StringComparer.OrdinalIgnoreCase))
        {
            var windowApi = new WindowApiLite(context, options.GetPlayerWindow);
            windowApi.SetEventManager(eventManager);
            engine.AddHostObject("window", windowApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: window");
        }

        // storage API
        if (permissions.Contains(PluginPermissionsV2.Storage, StringComparer.OrdinalIgnoreCase))
        {
            var storageApi = new StorageApiLite(context);
            engine.AddHostObject("storage", storageApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: storage");
        }

        // http API
        if (permissions.Contains(PluginPermissionsV2.Network, StringComparer.OrdinalIgnoreCase))
        {
            var httpApi = new HttpApi(pluginId, manifest.HttpAllowedUrls?.ToArray());
            engine.AddHostObject("http", httpApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: http");
        }

        // subtitle API
        if (permissions.Contains(PluginPermissionsV2.Subtitle, StringComparer.OrdinalIgnoreCase))
        {
            var subtitleApi = new SubtitleApiLite(context, engine);
            subtitleApi.SetEventManager(eventManager);
            engine.AddHostObject("subtitle", subtitleApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: subtitle");
        }

        // event API（始终暴露）
        var eventApi = new EventApiLite(context, eventManager);
        engine.AddHostObject("event", eventApi);
        LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: event");

        // hotkey API
        if (permissions.Contains(PluginPermissionsV2.Hotkey, StringComparer.OrdinalIgnoreCase))
        {
            var hotkeyApi = new HotkeyApi(pluginId);
            // 注意：ActionDispatcher 需要在 PluginHost 中设置
            engine.AddHostObject("hotkey", hotkeyApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: hotkey");
        }

        // webview API（需要 player 权限）
        if (permissions.Contains(PluginPermissionsV2.Player, StringComparer.OrdinalIgnoreCase))
        {
            var webviewApi = new WebViewApi(pluginId, options.GetPlayerWindow);
            engine.AddHostObject("webview", webviewApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: webview");
        }
    }

    /// <summary>
    /// 创建 Profile 信息对象
    /// </summary>
    private static object CreateProfileInfo(PluginEngineOptions options)
    {
        return new { id = options.ProfileId ?? "default", name = options.ProfileName ?? "Default",
                     directory = options.ProfileDirectory ?? string.Empty };
    }

#endregion

#region Private Methods - Global Functions

    /// <summary>
    /// 暴露全局便捷函数
    /// </summary>
    private static void ExposeGlobalFunctions(V8ScriptEngine engine)
    {
        // 暴露 sleep 函数
        // 使用委托包装，使其可以直接作为全局函数调用
        engine.AddHostObject("sleep", new Func<int, Task>(GlobalFunctions.Sleep));

        LogService.Instance.Debug("PluginEngine", "Global functions exposed: sleep");
    }

#endregion

#region Private Methods - C #Types

    /// <summary>
    /// 暴露 C# 类型供 JavaScript 直接实例化
    /// </summary>
    private static void ExposeCSharpTypes(V8ScriptEngine engine)
    {
        // 取消令牌相关类型
        engine.AddHostType("CancellationTokenSource", typeof(CancellationTokenSource));
        engine.AddHostType("CancellationToken", typeof(CancellationToken));

        // Task 类型
        engine.AddHostType("Task", typeof(Task));

        // 计时器类型
        engine.AddHostType("Timer", typeof(System.Timers.Timer));
        engine.AddHostType("Stopwatch", typeof(Stopwatch));

        LogService.Instance.Debug(
            "PluginEngine", "C# types exposed: CancellationTokenSource, CancellationToken, Task, Timer, Stopwatch");
    }

#endregion
}

/// <summary>
/// 插件引擎初始化选项
/// </summary>
public class PluginEngineOptions
{
    /// <summary>
    /// 当前 Profile ID
    /// </summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// 当前 Profile 名称
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// 当前 Profile 目录
    /// </summary>
    public string? ProfileDirectory { get; set; }

    /// <summary>
    /// 获取 PlayerWindow 的委托
    /// </summary>
    public Func<Views.Windows.PlayerWindow?>? GetPlayerWindow { get; set; }
}

#region Lite API Classes

/// <summary>
/// 轻量级插件上下文
/// 用于 PluginEngine 初始化时创建 API 实例
/// </summary>
public class PluginContextLite
{
    public string PluginId { get; }
    public string PluginDirectory { get; }
    public string ConfigDirectory { get; }
    public PluginManifest Manifest { get; }

    public PluginContextLite(string pluginId, string pluginDirectory, string configDirectory, PluginManifest manifest)
    {
        PluginId = pluginId;
        PluginDirectory = pluginDirectory;
        ConfigDirectory = configDirectory;
        Manifest = manifest;
    }
}

/// <summary>
/// 日志 API
/// </summary>
public class LogApi
{
    private readonly string _pluginId;

    public LogApi(string pluginId)
    {
        _pluginId = pluginId;
    }

    /// <summary>
    /// 输出信息日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void info(string message) => LogService.Instance.Info($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出警告日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void warn(string message) => LogService.Instance.Warn($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出错误日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void error(string message) => LogService.Instance.Error($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出调试日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void debug(string message) => LogService.Instance.Debug($"Plugin:{_pluginId}", message);

    // 大写版本别名（向后兼容）
    public void Info(string message) => info(message);
    public void Warn(string message) => warn(message);
    public void Error(string message) => error(message);
    public void Debug(string message) => debug(message);
}

/// <summary>
/// 插件信息代理（只读）
/// 暴露 plugin.json 中的元信息给 JS
/// </summary>
public class PluginInfoProxy
{
    private readonly PluginManifest _manifest;

    public PluginInfoProxy(PluginManifest manifest)
    {
        _manifest = manifest;
    }

    /// <summary>插件 ID</summary>
    public string id => _manifest.Id ?? "";

    /// <summary>插件名称</summary>
    public string name => _manifest.Name ?? "";

    /// <summary>插件版本</summary>
    public string version => _manifest.Version ?? "";

    /// <summary>插件描述</summary>
    public string description => _manifest.Description ?? "";

    /// <summary>插件作者</summary>
    public string author => _manifest.Author ?? "";

    /// <summary>入口文件</summary>
    public string main => _manifest.Main ?? "main.js";

    /// <summary>权限列表</summary>
    public string[] permissions => _manifest.Permissions?.ToArray() ?? Array.Empty<string>();
}

/// <summary>
/// Overlay API（轻量版）
/// </summary>
public class OverlayApiLite
{
    private readonly PluginContextLite _context;
    private readonly ConfigApi _configApi;

    public OverlayApiLite(PluginContextLite context, ConfigApi configApi)
    {
        _context = context;
        _configApi = configApi;
    }

    public void show()
    {
        var overlay = EnsureOverlay();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.Show());
    }

    public void hide()
    {
        var overlay = OverlayManager.Instance.GetOverlay(_context.PluginId);
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.Hide());
    }

    public void setPosition(double x, double y)
    {
        var overlay = EnsureOverlay();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.SetPosition((int)x, (int)y));
    }

    public void setSize(double width, double height)
    {
        var overlay = EnsureOverlay();
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.SetSize((int)width, (int)height));
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
            LogService.Instance.Error($"Plugin:{_context.PluginId}",
                                      $"setMarkerImage: Image file not found: {fullPath}");
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
                                                  $"Animation frame callback error: {ex.Message}");
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

/// <summary>
/// Overlay 绘图上下文
/// 提供 Canvas-like 绘图 API
/// </summary>
public class OverlayContext
{
    private readonly string _pluginId;

    // 绘图状态（小写属性名，Canvas 风格）
    public string fillStyle { get; set; } = "#000000";
    public string strokeStyle { get; set; } = "#000000";
    public double lineWidth { get; set; } = 1.0;
    public string font { get; set; } = "14px Arial";

    public OverlayContext(string pluginId)
    {
        _pluginId = pluginId;
    }

    /// <summary>
    /// 填充矩形
    /// </summary>
    public void fillRect(double x, double y, double width, double height)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  var options = new DrawRectOptions { Fill = fillStyle,
                                                                                                      Opacity = 1.0 };
                                                                  overlay.DrawRect(x, y, width, height, options);
                                                              });
    }

    /// <summary>
    /// 描边矩形
    /// </summary>
    public void strokeRect(double x, double y, double width, double height)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                var options = new DrawRectOptions { Stroke = strokeStyle, StrokeWidth = lineWidth, Opacity = 1.0 };
                overlay.DrawRect(x, y, width, height, options);
            });
    }

    /// <summary>
    /// 填充文本
    /// </summary>
    public void fillText(string text, double x, double y)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                // 解析字体大小
                double fontSize = 14;
                string fontFamily = "Arial";
                if (!string.IsNullOrEmpty(font))
                {
                    var parts = font.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var sizeStr = parts[0].Replace("px", "").Replace("pt", "");
                        if (double.TryParse(sizeStr, out var size))
                            fontSize = size;
                        fontFamily = string.Join(" ", parts.Skip(1));
                    }
                }

                var options = new DrawTextOptions { Color = fillStyle, FontSize = fontSize, FontFamily = fontFamily,
                                                    Opacity = 1.0 };
                overlay.DrawText(text, x, y, options);
            });
    }

    /// <summary>
    /// 绘制图片
    /// </summary>
    public void drawImage(string src, double x, double y, double? width = null, double? height = null)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                var options = new DrawImageOptions { Width = width ?? 0, Height = height ?? 0, Opacity = 1.0 };
                overlay.DrawImage(src, x, y, options);
            });
    }

    /// <summary>
    /// 清除画布
    /// </summary>
    public void clear()
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.ClearDrawingElements());
    }
}

/// <summary>
/// Player API（轻量版）
/// </summary>
public class PlayerApiLite
{
    private readonly PluginContextLite _context;
    private readonly Func<Views.Windows.PlayerWindow?>? _getPlayerWindow;
    private EventManager? _eventManager;

    public PlayerApiLite(PluginContextLite context, Func<Views.Windows.PlayerWindow?>? getPlayerWindow)
    {
        _context = context;
        _getPlayerWindow = getPlayerWindow;
    }

    public void SetEventManager(EventManager eventManager) => _eventManager = eventManager;

    /// <summary>
    /// 当前 URL（小写属性名，JavaScript 风格）
    /// </summary>
    public string url => _getPlayerWindow?.Invoke()?.CurrentUrl ?? string.Empty;

    /// <summary>
    /// 当前播放时间（秒）
    /// </summary>
    public double currentTime => 0; // TODO: 从 WebView 获取

    /// <summary>
    /// 视频总时长（秒）
    /// </summary>
    public double duration => 0; // TODO: 从 WebView 获取

    /// <summary>
    /// 当前音量（0.0-1.0）
    /// </summary>
    public double volume => 1.0; // TODO: 从 WebView 获取

    /// <summary>
    /// 当前播放速度
    /// </summary>
    public double playbackRate => 1.0; // TODO: 从 WebView 获取

    /// <summary>
    /// 是否静音
    /// </summary>
    public bool muted => false; // TODO: 从 WebView 获取

    /// <summary>
    /// 是否正在播放
    /// </summary>
    public bool playing => false; // TODO: 从 WebView 获取

    // 大写版本别名（向后兼容）
    public string Url => url;
    public double CurrentTime => currentTime;
    public double Duration => duration;
    public double Volume => volume;
    public double PlaybackRate => playbackRate;
    public bool Muted => muted;
    public bool Playing => playing;

    /// <summary>
    /// 开始播放
    /// </summary>
    public void play() => _getPlayerWindow?.Invoke()?.TogglePlayAsync();

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void pause() => _getPlayerWindow?.Invoke()?.TogglePlayAsync();

    /// <summary>
    /// 跳转到指定时间
    /// </summary>
    /// <param name="time">目标时间（秒）</param>
    public void seek(double time) => _getPlayerWindow?.Invoke()?.SeekAsync((int)time);

    /// <summary>
    /// 设置音量
    /// </summary>
    /// <param name="vol">音量（0.0-1.0）</param>
    public void setVolume(double vol)
    { /* TODO: 实现音量控制 */
    }

    /// <summary>
    /// 设置播放速度
    /// </summary>
    /// <param name="rate">播放速度</param>
    public void setPlaybackRate(double rate)
    { /* TODO: 实现播放速度控制 */
    }

    /// <summary>
    /// 设置静音状态
    /// </summary>
    /// <param name="mute">是否静音</param>
    public void setMuted(bool mute)
    { /* TODO: 实现静音控制 */
    }

    /// <summary>
    /// 导航到指定 URL
    /// </summary>
    /// <param name="targetUrl">目标 URL</param>
    /// <returns>Task</returns>
    public Task navigate(string targetUrl)
    {
        _getPlayerWindow?.Invoke()?.Navigate(targetUrl);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    /// <returns>Task</returns>
    public Task reload()
    {
        _getPlayerWindow?.Invoke()?.Refresh();
        return Task.CompletedTask;
    }

    // 大写版本别名（向后兼容）
    public void Play() => play();
    public void Pause() => pause();
    public void Seek(double time) => seek(time);
    public void SetVolume(double vol) => setVolume(vol);
    public void SetPlaybackRate(double rate) => setPlaybackRate(rate);
    public void SetMuted(bool mute) => setMuted(mute);
    public Task Navigate(string targetUrl) => navigate(targetUrl);
    public Task Reload() => reload();

    /// <summary>
    /// 注册事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数</param>
    /// <returns>监听器 ID</returns>
    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"player.{eventName}", callback) ?? -1;
    }

    // 大写版本别名
    public int On(string eventName, object callback) => on(eventName, callback);

    /// <summary>
    /// 取消事件监听
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="id">监听器 ID（可选）</param>
    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"player.{eventName}");
    }

    // 大写版本别名
    public void Off(string eventName, int? id = null) => off(eventName, id);
}

/// <summary>
/// Window API（轻量版）
/// </summary>
public class WindowApiLite
{
    private readonly PluginContextLite _context;
    private readonly Func<Views.Windows.PlayerWindow?>? _getPlayerWindow;
    private EventManager? _eventManager;

    public WindowApiLite(PluginContextLite context, Func<Views.Windows.PlayerWindow?>? getPlayerWindow)
    {
        _context = context;
        _getPlayerWindow = getPlayerWindow;
    }

    public void SetEventManager(EventManager eventManager) => _eventManager = eventManager;

    public double Opacity => _getPlayerWindow?.Invoke()?.Opacity ?? 1.0;
    public bool ClickThrough => _getPlayerWindow?.Invoke()?.IsClickThrough ?? false;
    public bool Topmost => _getPlayerWindow?.Invoke()?.Topmost ?? true;

    public object Bounds
    {
        get {
            var window = _getPlayerWindow?.Invoke();
            if (window == null)
                return new { x = 0, y = 0, width = 0, height = 0 };
            return new { x = (int)window.Left, y = (int)window.Top, width = (int)window.Width,
                         height = (int)window.Height };
        }
    }

    public void SetOpacity(double opacity)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Opacity = opacity);
    }

    public void SetClickThrough(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null && window.IsClickThrough != enabled)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.ToggleClickThrough());
    }

    public void SetTopmost(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Topmost = enabled);
    }

    public int On(string eventName, object callback)
    {
        return _eventManager?.On($"window.{eventName}", callback) ?? -1;
    }

    public void Off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"window.{eventName}");
    }
}

/// <summary>
/// Storage API（轻量版）
/// </summary>
public class StorageApiLite
{
    private readonly PluginContextLite _context;
    private readonly string _storagePath;

    public StorageApiLite(PluginContextLite context)
    {
        _context = context;
        _storagePath = Path.Combine(context.ConfigDirectory, "storage");
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    public bool Save(string key, object data)
    {
        try
        {
            var filePath = GetFilePath(key);
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", $"Storage save failed: {ex.Message}");
            return false;
        }
    }

    public object? Load(string key)
    {
        try
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
                return null;
            var json = File.ReadAllText(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<object>(json);
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", $"Storage load failed: {ex.Message}");
            return null;
        }
    }

    public bool Delete(string key)
    {
        try
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool Exists(string key) => File.Exists(GetFilePath(key));

    public string[] List()
    {
        if (!Directory.Exists(_storagePath))
            return Array.Empty<string>();
        return Directory.GetFiles(_storagePath, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
    }

    private string GetFilePath(string key) => Path.Combine(_storagePath, $"{key}.json");
}

/// <summary>
/// Subtitle API（轻量版）
/// </summary>
public class SubtitleApiLite
{
    private readonly PluginContextLite _context;
    private readonly V8ScriptEngine _engine;
    private EventManager? _eventManager;
    private bool _isSubscribed;

    public SubtitleApiLite(PluginContextLite context, V8ScriptEngine engine)
    {
        _context = context;
        _engine = engine;
    }

    public void SetEventManager(EventManager eventManager)
    {
        _eventManager = eventManager;
        SubscribeToService();
    }

    /// <summary>
    /// 订阅 SubtitleService 事件
    /// </summary>
    private void SubscribeToService()
    {
        if (_isSubscribed || _eventManager == null)
            return;

        SubtitleService.Instance.SubtitleChanged += OnSubtitleChanged;
        SubtitleService.Instance.SubtitleLoaded += OnSubtitleLoaded;
        SubtitleService.Instance.SubtitleCleared += OnSubtitleCleared;
        _isSubscribed = true;

        LogService.Instance.Debug($"Plugin:{_context.PluginId}", "SubtitleApi: EventManager set");
    }

    /// <summary>
    /// 取消订阅（清理时调用）
    /// </summary>
    public void Cleanup()
    {
        if (!_isSubscribed)
            return;

        SubtitleService.Instance.SubtitleChanged -= OnSubtitleChanged;
        SubtitleService.Instance.SubtitleLoaded -= OnSubtitleLoaded;
        SubtitleService.Instance.SubtitleCleared -= OnSubtitleCleared;
        _isSubscribed = false;
    }

    private void OnSubtitleChanged(object? sender, SubtitleEntry? e)
    {
        var jsEntry = e != null ? new { from = e.From, to = e.To, content = e.Content } : null;
        _eventManager?.Emit("subtitle.change", jsEntry);
    }

    private void OnSubtitleLoaded(object? sender, SubtitleData data)
    {
        // 创建原生 JS 数组，确保 forEach 等方法可用
        var jsBody = CreateJsArray(data.Body);
        var jsData = new { language = data.Language, body = jsBody, sourceUrl = data.SourceUrl };
        _eventManager?.Emit("subtitle.load", jsData);
    }

    /// <summary>
    /// 创建原生 JS 数组
    /// </summary>
    private object CreateJsArray(IEnumerable<SubtitleEntry> entries)
    {
        try
        {
            // 使用 V8 引擎创建原生 JS 数组
            dynamic jsArray = _engine.Evaluate("[]");
            foreach (var entry in entries)
            {
                jsArray.push(new { from = entry.From, to = entry.To, content = entry.Content });
            }
            return jsArray;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", $"CreateJsArray failed: {ex.Message}");
            // 回退到 C# 数组
            return entries.Select(e => (object) new { from = e.From, to = e.To, content = e.Content }).ToArray();
        }
    }

    private void OnSubtitleCleared(object? sender, EventArgs e)
    {
        _eventManager?.Emit("subtitle.clear", null);
    }

    // 属性和方法
    public bool HasSubtitles => SubtitleService.Instance.GetSubtitleData() != null;
    public bool hasSubtitles => HasSubtitles;

    public object? GetCurrent(double? time = null)
    {
        var entry = time.HasValue ? SubtitleService.Instance.GetSubtitleAt(time.Value) : null;
        if (entry == null)
            return null;
        return new { from = entry.From, to = entry.To, content = entry.Content };
    }
    public object? getCurrent(double? time = null) => GetCurrent(time);

    public object GetAll()
    {
        var entries = SubtitleService.Instance.GetAllSubtitles();
        return CreateJsArray(entries);
    }
    public object getAll() => GetAll();

    public int On(string eventName, object callback)
    {
        return _eventManager?.On($"subtitle.{eventName}", callback) ?? -1;
    }
    public int on(string eventName, object callback) => On(eventName, callback);

    public void Off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"subtitle.{eventName}");
    }
    public void off(string eventName, int? id = null) => Off(eventName, id);
}

/// <summary>
/// Event API（轻量版）
/// </summary>
public class EventApiLite
{
    private readonly PluginContextLite _context;
    private readonly EventManager _eventManager;

    public EventApiLite(PluginContextLite context, EventManager eventManager)
    {
        _context = context;
        _eventManager = eventManager;
    }

    public int On(string eventName, object callback)
    {
        return _eventManager.On(eventName, callback);
    }

    public void Off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager.Off(id.Value);
        else
            _eventManager.Off(eventName);
    }

    public void Emit(string eventName, object? data = null)
    {
        _eventManager.Emit(eventName, data);
    }
}

/// <summary>
/// 插件权限常量（内部使用，避免与 Models.Plugin.PluginPermissions 冲突）
/// </summary>
internal static class PluginPermissionsV2
{
    public const string Overlay = "overlay";
    public const string Player = "player";
    public const string Window = "window";
    public const string Storage = "storage";
    public const string Network = "network";
    public const string Subtitle = "subtitle";
    public const string Events = "events";
    public const string Hotkey = "hotkey";
    public const string Audio = "audio";
}

#endregion

}