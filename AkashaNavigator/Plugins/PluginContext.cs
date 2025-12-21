using System;
using System.IO;
using AkashaNavigator.Models.Plugin;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 插件上下文
/// 每个插件实例的运行时上下文，封装 V8 Engine 实例
/// </summary>
public class PluginContext : IDisposable
{
#region Fields

    private V8ScriptEngine? _jsEngine;
    private bool _disposed;
    private bool _isLoaded;
    private bool _useNewEngine;
    private PluginConfig? _config;
    private PluginEngineOptions? _engineOptions;

#endregion

#region Properties

    /// <summary>
    /// 插件 ID
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// 插件清单
    /// </summary>
    public PluginManifest Manifest { get; }

    /// <summary>
    /// 插件目录路径（源码目录）
    /// </summary>
    public string PluginDirectory { get; }

    /// <summary>
    /// 插件配置目录路径（用户数据目录，可写）
    /// 如果未设置，则使用 PluginDirectory
    /// </summary>
    public string ConfigDirectory { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否已加载（onLoad 已调用）
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 是否使用新引擎（v2 API）
    /// </summary>
    public bool UseNewEngine => _useNewEngine;

#endregion

#region Constructor

    /// <summary>
    /// 创建插件上下文（旧版构造函数，保留向后兼容）
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件目录（源码目录）</param>
    public PluginContext(PluginManifest manifest, string pluginDirectory)
        : this(manifest, pluginDirectory, null, null, false)
    {
    }

    /// <summary>
    /// 创建插件上下文（新版构造函数，使用 PluginEngine 初始化）
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件目录（源码目录）</param>
    /// <param name="config">插件配置</param>
    /// <param name="options">引擎选项</param>
    /// <param name="useNewEngine">是否使用新引擎</param>
    public PluginContext(PluginManifest manifest, string pluginDirectory, PluginConfig? config,
                         PluginEngineOptions? options, bool useNewEngine)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        PluginId = manifest.Id ?? throw new ArgumentException("插件 ID 不能为空");
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ConfigDirectory = pluginDirectory; // 默认配置目录与源码目录相同
        _config = config;
        _engineOptions = options;
        _useNewEngine = useNewEngine;

        InitializeEngine();
    }

#endregion

#region Factory Methods

    /// <summary>
    /// 创建使用新引擎（v2 API）的插件上下文
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件源码目录</param>
    /// <param name="configDirectory">插件配置目录</param>
    /// <param name="config">插件配置</param>
    /// <param name="options">引擎选项</param>
    /// <returns>插件上下文实例</returns>
    public static PluginContext CreateWithNewEngine(PluginManifest manifest, string pluginDirectory,
                                                    string configDirectory, PluginConfig config,
                                                    PluginEngineOptions? options = null)
    {
        var context = new PluginContext(manifest, pluginDirectory, config, options,
                                        useNewEngine: true) { ConfigDirectory = configDirectory };
        return context;
    }

#endregion

#region Engine Initialization

    /// <summary>
    /// 初始化 V8 引擎
    /// </summary>
    private void InitializeEngine()
    {
        if (_useNewEngine && _config != null)
        {
            // 使用新的 PluginEngine 初始化
            InitializeWithPluginEngine();
        }
        else
        {
            // 使用旧的初始化方式（向后兼容）
            InitializeLegacyEngine();
        }
    }

    /// <summary>
    /// 使用 PluginEngine 初始化引擎（新 v2 API）
    /// </summary>
    private void InitializeWithPluginEngine()
    {
        // 创建 V8 引擎，启用关键标志
        var engineFlags = V8ScriptEngineFlags.EnableDebugging | V8ScriptEngineFlags.UseCaseInsensitiveMemberBinding |
                          V8ScriptEngineFlags.EnableTaskPromiseConversion;

        _jsEngine = new V8ScriptEngine(engineFlags);

        // 配置内存限制
        _jsEngine.MaxRuntimeHeapSize = (UIntPtr)50_000_000;     // 50MB
        _jsEngine.MaxRuntimeStackUsage = (UIntPtr)(100 * 1024); // 100KB stack

        // 注入基础 console 用于调试
        _jsEngine.AddHostObject("console", new ConsoleProxy(PluginId));

        // 使用 PluginEngine 初始化所有 API
        var libraryPaths = Manifest.Library?.ToArray();
        PluginEngine.InitializeEngine(_jsEngine, PluginDirectory, ConfigDirectory, libraryPaths, _config!, Manifest,
                                      _engineOptions);

        Log($"使用新引擎初始化完成 (library paths: {libraryPaths?.Length ?? 0}, http_allowed_urls: {Manifest.HttpAllowedUrls?.Count ?? 0})");
    }

    /// <summary>
    /// 使用旧方式初始化引擎（向后兼容）
    /// </summary>
    private void InitializeLegacyEngine()
    {
        _jsEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging);

        // 限制内存使用 (50MB)
        _jsEngine.MaxRuntimeHeapSize = (UIntPtr)50_000_000;

        // 限制递归深度
        _jsEngine.MaxRuntimeStackUsage = (UIntPtr)(100 * 1024); // 100KB stack

        // 注入基础 console 用于调试
        _jsEngine.AddHostObject("console", new ConsoleProxy(PluginId));
    }

#endregion

#region Script Execution

    /// <summary>
    /// 加载并执行插件脚本
    /// </summary>
    /// <returns>是否成功</returns>
    public bool LoadScript()
    {
        if (_disposed || _jsEngine == null)
            return false;

        var mainFile = Path.Combine(PluginDirectory, Manifest.Main ?? "main.js");
        if (!File.Exists(mainFile))
        {
            LastError = $"入口文件不存在: {mainFile}";
            Log(LastError);
            return false;
        }

        try
        {
            var script = File.ReadAllText(mainFile);
            _jsEngine.Execute(script);
            return true;
        }
        catch (ScriptEngineException ex)
        {
            LastError = $"JavaScript 错误: {ex.Message}";
            Log(LastError);
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"加载脚本失败: {ex.Message}";
            Log(LastError);
            return false;
        }
    }

    /// <summary>
    /// 调用插件的 JavaScript 函数
    /// </summary>
    /// <param name="functionName">函数名</param>
    /// <param name="args">参数</param>
    /// <returns>调用结果（成功返回 true）</returns>
    public bool InvokeFunction(string functionName, params object[] args)
    {
        if (_disposed || _jsEngine == null)
            return false;

        try
        {
            if (!HasFunction(functionName))
            {
                // 函数不存在不算错误，只是跳过
                return true;
            }

            _jsEngine.Invoke(functionName, args);
            return true;
        }
        catch (ScriptEngineException ex)
        {
            LastError = $"调用 {functionName} 失败: {ex.Message}";
            Log(LastError);
            if (ex.InnerException != null)
            {
                Log($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"调用 {functionName} 异常: {ex.GetType().Name} - {ex.Message}";
            Log(LastError);
            if (ex.InnerException != null)
            {
                Log($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// 检查函数是否存在
    /// </summary>
    public bool HasFunction(string functionName)
    {
        if (_disposed || _jsEngine == null)
            return false;

        try
        {
            var result = _jsEngine.Evaluate($"typeof {functionName} === 'function'");
            return result is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 执行 JavaScript 表达式并返回结果
    /// </summary>
    /// <param name="expression">JS 表达式</param>
    /// <returns>执行结果</returns>
    public object? Evaluate(string expression)
    {
        if (_disposed || _jsEngine == null)
            return null;

        try
        {
            return _jsEngine.Evaluate(expression);
        }
        catch (Exception ex)
        {
            Log($"Evaluate 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 创建 JavaScript 原生数组
    /// </summary>
    /// <returns>JS Array 对象</returns>
    public dynamic? CreateJsArray()
    {
        if (_disposed || _jsEngine == null)
            return null;

        try
        {
            return _jsEngine.Evaluate("[]");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 创建 JavaScript 原生对象
    /// </summary>
    /// <returns>JS Object</returns>
    public dynamic? CreateJsObject()
    {
        if (_disposed || _jsEngine == null)
            return null;

        try
        {
            return _jsEngine.Evaluate("({})");
        }
        catch
        {
            return null;
        }
    }

#endregion

#region Memory Management

    /// <summary>
    /// 主动触发 V8 垃圾回收
    /// </summary>
    public void CollectGarbage()
    {
        if (_disposed || _jsEngine == null)
            return;

        try
        {
            _jsEngine.CollectGarbage(true);
            Log("已触发 V8 垃圾回收");
        }
        catch (Exception ex)
        {
            Log($"垃圾回收失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取当前堆内存使用量（字节）
    /// </summary>
    public ulong GetHeapSize()
    {
        if (_disposed || _jsEngine == null)
            return 0;

        try
        {
            return _jsEngine.GetRuntimeHeapInfo().UsedHeapSize;
        }
        catch
        {
            return 0;
        }
    }

#endregion

#region Lifecycle

    /// <summary>
    /// 设置插件 API 并注入到 JS 引擎（旧版 API，向后兼容）
    /// </summary>
    /// <param name="api">插件 API 对象</param>
    public void SetApi(PluginApi api)
    {
        if (_disposed || _jsEngine == null || api == null)
            return;

        // 新引擎不需要注入 api 对象（已通过 PluginEngine 暴露全局对象）
        if (_useNewEngine)
        {
            Log("新引擎模式：跳过 SetApi（API 已作为全局对象暴露）");
            return;
        }

        // 将 API 注入到 JS 全局作用域（旧版）
        _jsEngine.AddHostObject("api", api);
    }

    /// <summary>
    /// 调用 onLoad 生命周期函数
    /// </summary>
    /// <param name="api">插件 API 对象（旧版需要，新版可选）</param>
    /// <returns>是否成功</returns>
    public bool CallOnLoad(object? api = null)
    {
        if (_isLoaded)
            return true;

        // 如果提供了 API 且不是新引擎模式，先注入到 JS 引擎
        if (api is PluginApi pluginApi && !_useNewEngine)
        {
            SetApi(pluginApi);
        }

        bool result;
        if (_useNewEngine)
        {
            // 新引擎模式：onLoad 不需要参数（API 已作为全局对象暴露）
            result = InvokeFunction("onLoad");
        }
        else
        {
            // 旧版模式：传递 api 参数
            result = api != null ? InvokeFunction("onLoad", api) : InvokeFunction("onLoad");
        }

        if (result)
        {
            _isLoaded = true;
        }
        return result;
    }

    /// <summary>
    /// 调用 onUnload 生命周期函数
    /// </summary>
    /// <param name="api">插件 API 对象（旧版需要，新版可选）</param>
    /// <returns>是否成功</returns>
    public bool CallOnUnload(object? api = null)
    {
        if (!_isLoaded)
            return true;

        bool result;
        if (_useNewEngine)
        {
            // 新引擎模式：onUnload 不需要参数
            result = InvokeFunction("onUnload");
        }
        else
        {
            // 旧版模式：如果提供了 API，传递给 onUnload；否则使用全局注入的 api
            result = api != null ? InvokeFunction("onUnload", api) : InvokeFunction("onUnload");
        }

        _isLoaded = false;
        return result;
    }

#endregion

#region Logging

    /// <summary>
    /// 记录日志
    /// </summary>
    private void Log(string message)
    {
        Services.LogService.Instance.Info($"Plugin:{PluginId}", message);
    }

#endregion

#region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // 调用 onUnload
            if (_isLoaded)
            {
                CallOnUnload();
            }

            // V8 引擎需要显式释放
            _jsEngine?.Dispose();
            _jsEngine = null;
        }

        _disposed = true;
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~PluginContext()
    {
        Dispose(false);
    }

#endregion

#region Console Proxy

    /// <summary>
    /// Console 代理类，用于 JS 中的 console.log 等
    /// </summary>
    private class ConsoleProxy
    {
        private readonly string _pluginId;

        public ConsoleProxy(string pluginId)
        {
            _pluginId = pluginId;
        }

        public void log(object? msg) => Services.LogService.Instance.Info($"Plugin:{_pluginId}", $"[JS] {msg}");
        public void warn(object? msg) => Services.LogService.Instance.Warn($"Plugin:{_pluginId}", $"[JS WARN] {msg}");
        public void error(object? msg) => Services.LogService.Instance.Error($"Plugin:{_pluginId}",
                                                                             $"[JS ERROR] {msg}");
        public void info(object? msg) => log(msg);
        public void debug(object? msg) => Services.LogService.Instance.Debug($"Plugin:{_pluginId}",
                                                                             $"[JS DEBUG] {msg}");
    }

#endregion
}
}
