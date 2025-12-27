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
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Apis;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Core
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

        LogService.Instance.Debug("PluginEngine", "Module search paths: {SearchPath}",
                                  engine.DocumentSettings.SearchPath);
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
        var tempContext = new PluginContext(pluginId, pluginDir, configDir, manifest);

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
    private static void ExposeApiObjects(V8ScriptEngine engine, PluginContext context, ConfigApi configApi,
                                         EventManager eventManager, PluginManifest manifest,
                                         PluginEngineOptions options)
    {
        var permissions = manifest.Permissions ?? new List<string>();
        var pluginId = context.PluginId;

        // overlay API
        if (permissions.Contains(PluginPermissions.Overlay, StringComparer.OrdinalIgnoreCase))
        {
            var overlayApi = new OverlayApi(context, configApi);
            engine.AddHostObject("overlay", overlayApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: overlay");
        }

        // player API
        if (permissions.Contains(PluginPermissions.Player, StringComparer.OrdinalIgnoreCase))
        {
            var playerApi = new PlayerApi(context, options.GetPlayerWindow);
            playerApi.SetEventManager(eventManager);
            engine.AddHostObject("player", playerApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: player");
        }

        // window API
        if (permissions.Contains(PluginPermissions.Window, StringComparer.OrdinalIgnoreCase))
        {
            var windowApi = new WindowApi(context, options.GetPlayerWindow);
            windowApi.SetEventManager(eventManager);
            engine.AddHostObject("window", windowApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: window");
        }

        // storage API
        if (permissions.Contains(PluginPermissions.Storage, StringComparer.OrdinalIgnoreCase))
        {
            var storageApi = new StorageApi(context);
            engine.AddHostObject("storage", storageApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: storage");
        }

        // http API
        if (permissions.Contains(PluginPermissions.Network, StringComparer.OrdinalIgnoreCase))
        {
            var httpApi = new HttpApi(pluginId, manifest.HttpAllowedUrls?.ToArray());
            engine.AddHostObject("http", httpApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: http");
        }

        // subtitle API
        if (permissions.Contains(PluginPermissions.Subtitle, StringComparer.OrdinalIgnoreCase))
        {
            var subtitleApi = new SubtitleApi(context, engine);
            subtitleApi.SetEventManager(eventManager);
            engine.AddHostObject("subtitle", subtitleApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: subtitle");
        }

        // event API（始终暴露）
        var eventApi = new EventApi(context, eventManager);
        engine.AddHostObject("event", eventApi);
        LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: event");

        // hotkey API
        if (permissions.Contains(PluginPermissions.Audio, StringComparer.OrdinalIgnoreCase))
        {
            var hotkeyApi = new HotkeyApi(pluginId);
            // 注意：ActionDispatcher 需要在 PluginHost 中设置
            engine.AddHostObject("hotkey", hotkeyApi);
            LogService.Instance.Debug($"PluginEngine:{pluginId}", "Exposed: hotkey");
        }

        // webview API（需要 player 权限）
        if (permissions.Contains(PluginPermissions.Player, StringComparer.OrdinalIgnoreCase))
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
}