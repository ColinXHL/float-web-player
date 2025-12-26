using System;
using System.IO;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Core
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
    /// V8 脚本引擎实例（用于 API 初始化）
    /// </summary>
    public V8ScriptEngine? Engine => _jsEngine;

#endregion

#region Constructor

    /// <summary>
    /// 创建插件上下文（测试用简化构造函数）
    /// 仅初始化基础 V8 引擎，不注入 Plugin API
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件目录（源码目录）</param>
    public PluginContext(PluginManifest manifest, string pluginDirectory)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        PluginId = manifest.Id ?? throw new ArgumentException("插件 ID 不能为空");
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ConfigDirectory = pluginDirectory;

        InitializeBasicEngine();
    }

    /// <summary>
    /// 创建插件上下文（用于 PluginEngine API 初始化）
    /// 不初始化 V8 引擎，仅作为数据容器
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="pluginDirectory">插件目录（源码目录）</param>
    /// <param name="configDirectory">配置目录</param>
    /// <param name="manifest">插件清单</param>
    public PluginContext(string pluginId, string pluginDirectory, string configDirectory, PluginManifest manifest)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ConfigDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        // 不初始化引擎，仅作为数据容器使用
    }

    /// <summary>
    /// 创建插件上下文（生产用完整构造函数）
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件目录（源码目录）</param>
    /// <param name="config">插件配置</param>
    /// <param name="options">引擎选项</param>
    private PluginContext(PluginManifest manifest, string pluginDirectory, PluginConfig config,
                          PluginEngineOptions? options)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        PluginId = manifest.Id ?? throw new ArgumentException("插件 ID 不能为空");
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        ConfigDirectory = pluginDirectory;
        _config = config;
        _engineOptions = options;

        InitializeWithPluginEngine();
    }

#endregion

#region Factory Methods

    /// <summary>
    /// 创建插件上下文（生产环境使用）
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="pluginDirectory">插件源码目录</param>
    /// <param name="configDirectory">插件配置目录</param>
    /// <param name="config">插件配置</param>
    /// <param name="options">引擎选项</param>
    /// <returns>插件上下文实例</returns>
    public static PluginContext Create(PluginManifest manifest, string pluginDirectory, string configDirectory,
                                       PluginConfig config, PluginEngineOptions? options = null)
    {
        var context =
            new PluginContext(manifest, pluginDirectory, config, options) { ConfigDirectory = configDirectory };
        return context;
    }

#endregion

#region Engine Initialization

    /// <summary>
    /// 初始化基础 V8 引擎（测试用）
    /// </summary>
    private void InitializeBasicEngine()
    {
        _jsEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging);
        _jsEngine.MaxRuntimeHeapSize = (UIntPtr)50_000_000;
        _jsEngine.MaxRuntimeStackUsage = (UIntPtr)(100 * 1024);
        _jsEngine.AddHostObject("console", new ConsoleProxy(PluginId));
    }

    /// <summary>
    /// 使用 PluginEngine 初始化引擎（生产用）
    /// </summary>
    private void InitializeWithPluginEngine()
    {
        var engineFlags = V8ScriptEngineFlags.EnableDebugging | V8ScriptEngineFlags.UseCaseInsensitiveMemberBinding |
                          V8ScriptEngineFlags.EnableTaskPromiseConversion;

        _jsEngine = new V8ScriptEngine(engineFlags);
        _jsEngine.MaxRuntimeHeapSize = (UIntPtr)50_000_000;
        _jsEngine.MaxRuntimeStackUsage = (UIntPtr)(100 * 1024);
        _jsEngine.AddHostObject("console", new ConsoleProxy(PluginId));

        var libraryPaths = Manifest.Library?.ToArray();
        PluginEngine.InitializeEngine(_jsEngine, PluginDirectory, ConfigDirectory, libraryPaths, _config!, Manifest,
                                      _engineOptions);

        LogInfo("引擎初始化完成 (library paths: {LibraryPathCount}, http_allowed_urls: {HttpAllowedUrlCount})",
                libraryPaths?.Length ?? 0, Manifest.HttpAllowedUrls?.Count ?? 0);
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
            LastError = "入口文件不存在";
            Log("入口文件不存在: {MainFile}", mainFile);
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
            LastError = "JavaScript 错误";
            Log("JavaScript 错误: {ErrorMessage}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            LastError = "加载脚本失败";
            Log("加载脚本失败: {ErrorMessage}", ex.Message);
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
            LastError = "调用函数失败";
            Log("调用 {FunctionName} 失败: {ErrorMessage}", functionName, ex.Message);
            if (ex.InnerException != null)
            {
                Log("  内部异常: {ExceptionType} - {InnerMessage}", ex.InnerException.GetType().Name,
                    ex.InnerException.Message);
            }
            return false;
        }
        catch (Exception ex)
        {
            LastError = "调用函数异常";
            Log("调用 {FunctionName} 异常: {ExceptionType} - {ErrorMessage}", functionName, ex.GetType().Name,
                ex.Message);
            if (ex.InnerException != null)
            {
                Log("  内部异常: {ExceptionType} - {InnerMessage}", ex.InnerException.GetType().Name,
                    ex.InnerException.Message);
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
            var result = _jsEngine.Evaluate("typeof " + functionName + " === 'function'");
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
            Log("Evaluate 失败: {ErrorMessage}", ex.Message);
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
            LogInfo("已触发 V8 垃圾回收");
        }
        catch (Exception ex)
        {
            Log("垃圾回收失败: {ErrorMessage}", ex.Message);
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
    /// 调用 onLoad 生命周期函数
    /// </summary>
    /// <returns>是否成功</returns>
    public bool CallOnLoad()
    {
        if (_isLoaded)
            return true;

        var result = InvokeFunction("onLoad");
        if (result)
        {
            _isLoaded = true;
        }
        return result;
    }

    /// <summary>
    /// 调用 onUnload 生命周期函数
    /// </summary>
    /// <returns>是否成功</returns>
    public bool CallOnUnload()
    {
        if (!_isLoaded)
            return true;

        var result = InvokeFunction("onUnload");
        _isLoaded = false;
        return result;
    }

#endregion

#region Logging

    /// <summary>
    /// 记录日志（参数化模板）
    /// 用于插件内部的技术日志，使用 Debug 级别
    /// </summary>
    private void Log(string template, params object[] args)
    {
        Services.LogService.Instance.Debug($"Plugin:{PluginId}", template, args);
    }

    /// <summary>
    /// 记录用户可见的信息日志
    /// </summary>
    private void LogInfo(string template, params object[] args)
    {
        Services.LogService.Instance.Info($"Plugin:{PluginId}", template, args);
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

        public void log(object? msg) => Services.LogService.Instance.Info("Plugin:{PluginId}", "[JS] {Message}",
                                                                          _pluginId, msg);
        public void warn(object? msg) => Services.LogService.Instance.Warn("Plugin:{PluginId}", "[JS WARN] {Message}",
                                                                           _pluginId, msg);
        public void error(object? msg) => Services.LogService.Instance.Error("Plugin:{PluginId}",
                                                                             "[JS ERROR] {Message}", _pluginId, msg);
        public void info(object? msg) => log(msg);
        public void debug(object? msg) => Services.LogService.Instance.Debug("Plugin:{PluginId}",
                                                                             "[JS DEBUG] {Message}", _pluginId, msg);
    }

#endregion
}
}
