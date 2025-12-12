using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace FloatWebPlayer.Helpers
{
    /// <summary>
    /// WebView2 脚本注入管理器
    /// 负责读取嵌入资源（CSS/JS）并注入到 WebView2
    /// </summary>
    public static class ScriptInjector
    {
        #region Constants

        /// <summary>
        /// CSS 嵌入资源名称
        /// </summary>
        private const string StylesResourceName = "FloatWebPlayer.Scripts.InjectedStyles.css";

        /// <summary>
        /// JS 嵌入资源名称
        /// </summary>
        private const string ScriptsResourceName = "FloatWebPlayer.Scripts.InjectedScripts.js";

        #endregion

        #region Cache

        private static string? _cachedStyles;
        private static string? _cachedScripts;

        #endregion

        #region Public Methods

        /// <summary>
        /// 注入所有样式和脚本到 WebView2
        /// 使用 AddScriptToExecuteOnDocumentCreatedAsync API，
        /// 会在每次文档创建时自动执行
        /// </summary>
        /// <param name="webView">WebView2 控件</param>
        public static async Task InjectAllAsync(WebView2 webView)
        {
            // 注入 CSS（通过 JS 动态创建 style 元素）
            var cssInjectionScript = BuildCssInjectionScript();
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(cssInjectionScript);

            // 注入 JS
            var script = GetEmbeddedResource(ScriptsResourceName, ref _cachedScripts);
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
        }

        /// <summary>
        /// 清除缓存（用于热重载场景）
        /// </summary>
        public static void ClearCache()
        {
            _cachedStyles = null;
            _cachedScripts = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 构建 CSS 注入脚本
        /// 将 CSS 内容包装为 JS 代码，动态创建 style 元素
        /// 包含 DOM 就绪检测，确保在 document.head 可用后再注入
        /// </summary>
        private static string BuildCssInjectionScript()
        {
            var css = GetEmbeddedResource(StylesResourceName, ref _cachedStyles);
            
            // 转义 CSS 中的特殊字符
            var escapedCss = css
                .Replace("\\", "\\\\")
                .Replace("`", "\\`")
                .Replace("$", "\\$");

            return $@"
(function() {{
    'use strict';
    
    function injectStyles() {{
        // 防止重复注入
        if (document.getElementById('float-player-styles')) return;
        
        // 等待 document.head 可用
        var target = document.head || document.documentElement;
        if (!target) {{
            if (document.readyState === 'loading') {{
                document.addEventListener('DOMContentLoaded', injectStyles);
            }} else {{
                setTimeout(injectStyles, 10);
            }}
            return;
        }}
        
        var style = document.createElement('style');
        style.id = 'float-player-styles';
        style.textContent = `{escapedCss}`;
        target.appendChild(style);
    }}
    
    injectStyles();
}})();";
        }

        /// <summary>
        /// 从嵌入资源读取内容
        /// </summary>
        private static string GetEmbeddedResource(string resourceName, ref string? cache)
        {
            if (cache != null)
            {
                return cache;
            }

            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // 列出所有可用的嵌入资源以便调试
                var availableResources = string.Join("\n", assembly.GetManifestResourceNames());
                throw new FileNotFoundException(
                    $"嵌入资源未找到: {resourceName}\n" +
                    $"可用的嵌入资源:\n{availableResources}"
                );
            }

            using var reader = new StreamReader(stream);
            cache = reader.ReadToEnd();

            return cache;
        }

        #endregion
    }
}
