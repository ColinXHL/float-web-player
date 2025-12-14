using System;
using System.IO;
using FloatWebPlayer.Models;
using Jint;
using Jint.Runtime;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 插件上下文
    /// 每个插件实例的运行时上下文，封装 Jint Engine 实例
    /// </summary>
    public class PluginContext : IDisposable
    {
        #region Fields

        private Engine? _jsEngine;
        private bool _disposed;
        private bool _isLoaded;

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
        /// 插件目录路径
        /// </summary>
        public string PluginDirectory { get; }

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

        #endregion


        #region Constructor

        /// <summary>
        /// 创建插件上下文
        /// </summary>
        /// <param name="manifest">插件清单</param>
        /// <param name="pluginDirectory">插件目录</param>
        public PluginContext(PluginManifest manifest, string pluginDirectory)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            PluginId = manifest.Id ?? throw new ArgumentException("插件 ID 不能为空");
            PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));

            InitializeEngine();
        }

        #endregion

        #region Engine Initialization

        /// <summary>
        /// 初始化 Jint 引擎
        /// </summary>
        private void InitializeEngine()
        {
            _jsEngine = new Engine(options =>
            {
                // 限制执行时间（防止无限循环）
                options.TimeoutInterval(TimeSpan.FromSeconds(30));
                
                // 限制递归深度
                options.LimitRecursion(100);
                
                // 限制内存使用
                options.LimitMemory(50_000_000); // 50MB
                
                // 启用严格模式
                options.Strict();
            });

            // 注入基础 console.log 用于调试
            _jsEngine.SetValue("console", new
            {
                log = new Action<object>(msg => Log($"[JS] {msg}")),
                warn = new Action<object>(msg => Log($"[JS WARN] {msg}")),
                error = new Action<object>(msg => Log($"[JS ERROR] {msg}"))
            });
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
            catch (JavaScriptException ex)
            {
                LastError = $"JavaScript 错误: {ex.Message} (行 {ex.Location.Start.Line})";
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
                var func = _jsEngine.GetValue(functionName);
                if (func.IsUndefined() || !func.IsObject())
                {
                    // 函数不存在不算错误，只是跳过
                    return true;
                }

                _jsEngine.Invoke(functionName, args);
                return true;
            }
            catch (JavaScriptException ex)
            {
                LastError = $"调用 {functionName} 失败: {ex.Message}";
                Log(LastError);
                // 异常被捕获，不影响主程序
                return false;
            }
            catch (Exception ex)
            {
                LastError = $"调用 {functionName} 异常: {ex.Message}";
                Log(LastError);
                // 异常被捕获，不影响主程序
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
                var func = _jsEngine.GetValue(functionName);
                return !func.IsUndefined() && func.IsObject();
            }
            catch
            {
                return false;
            }
        }

        #endregion


        #region Lifecycle

        /// <summary>
        /// 设置插件 API 并注入到 JS 引擎
        /// </summary>
        /// <param name="api">插件 API 对象</param>
        public void SetApi(PluginApi api)
        {
            if (_disposed || _jsEngine == null || api == null)
                return;

            // 将 API 注入到 JS 全局作用域
            _jsEngine.SetValue("api", api);
        }

        /// <summary>
        /// 调用 onLoad 生命周期函数
        /// </summary>
        /// <param name="api">插件 API 对象</param>
        /// <returns>是否成功</returns>
        public bool CallOnLoad(object? api = null)
        {
            if (_isLoaded)
                return true;

            // 如果提供了 API，先注入到 JS 引擎
            if (api is PluginApi pluginApi)
            {
                SetApi(pluginApi);
            }

            var result = api != null 
                ? InvokeFunction("onLoad", api) 
                : InvokeFunction("onLoad");
            
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

                // Jint Engine 不需要显式释放，但清空引用
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
    }
}
