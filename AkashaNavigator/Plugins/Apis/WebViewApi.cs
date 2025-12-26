using System;
using System.Threading.Tasks;
using System.Windows;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// WebView API
/// 允许插件向 WebView2 注入 CSS 和 JavaScript
/// </summary>
/// <remarks>
/// 设计说明：
/// - 支持 CSS 和 JavaScript 注入
/// - 支持不同的注入时机（immediate, onLoad, onDOMReady）
/// - executeScript 返回 Promise 以支持 async/await
/// </remarks>
public class WebViewApi
{
#region Fields

    private readonly string _pluginId;
    private readonly Func<PlayerWindow?>? _getPlayerWindow;

#endregion

#region Constructor

    /// <summary>
    /// 创建 WebView API 实例
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="getPlayerWindow">获取 PlayerWindow 的委托</param>
    public WebViewApi(string pluginId, Func<PlayerWindow?>? getPlayerWindow = null)
    {
        _pluginId = pluginId;
        _getPlayerWindow = getPlayerWindow;
    }

#endregion

#region Public Methods

    /// <summary>
    /// 注入 CSS 样式
    /// </summary>
    /// <param name="css">CSS 代码</param>
    /// <param name="options">注入选项（可选）</param>
    /// <returns>是否成功</returns>
    public bool InjectCSS(string css, object? options = null)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.InjectCSS: css is empty", _pluginId);
            return false;
        }

        var timing = ParseTiming(options);

        try
        {
            var playerWindow = GetPlayerWindow();
            if (playerWindow?.WebView?.CoreWebView2 == null)
            {
                LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.InjectCSS: WebView not available", _pluginId);
                return false;
            }

            // 转义 CSS 中的特殊字符
            var escapedCss = EscapeForJavaScript(css);

            // 构建注入脚本
            var script = $@"
                    (function() {{
                        var style = document.createElement('style');
                        style.type = 'text/css';
                        style.setAttribute('data-plugin', '{_pluginId}');
                        style.textContent = `{escapedCss}`;
                        document.head.appendChild(style);
                        return true;
                    }})();
                ";

            // 根据时机执行注入
            ExecuteWithTiming(playerWindow, script, timing);

            LogService.Instance.Debug("Plugin:{PluginId}", "CSS injected (timing: {Timing})", _pluginId, timing);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Plugin:{PluginId}", "WebViewApi.InjectCSS error: {ErrorMessage}", _pluginId,
                                      ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 注入 JavaScript 脚本
    /// </summary>
    /// <param name="script">JavaScript 代码</param>
    /// <param name="options">注入选项（可选）</param>
    /// <returns>是否成功</returns>
    public bool InjectScript(string script, object? options = null)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.InjectScript: script is empty", _pluginId);
            return false;
        }

        var timing = ParseTiming(options);

        try
        {
            var playerWindow = GetPlayerWindow();
            if (playerWindow?.WebView?.CoreWebView2 == null)
            {
                LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.InjectScript: WebView not available",
                                         _pluginId);
                return false;
            }

            // 转义脚本中的特殊字符
            var escapedScript = EscapeForJavaScript(script);

            // 构建注入脚本（包装在 IIFE 中以隔离作用域）
            var wrappedScript = $@"
                    (function() {{
                        try {{
                            {escapedScript}
                        }} catch (e) {{
                            console.error('[Plugin:{_pluginId}] Script error:', e);
                        }}
                    }})();
                ";

            // 根据时机执行注入
            ExecuteWithTiming(playerWindow, wrappedScript, timing);

            LogService.Instance.Debug("Plugin:{PluginId}", "Script injected (timing: {Timing})", _pluginId, timing);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Plugin:{PluginId}", "WebViewApi.InjectScript error: {ErrorMessage}", _pluginId,
                                      ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 执行 JavaScript 脚本并返回结果
    /// </summary>
    /// <param name="script">JavaScript 代码</param>
    /// <returns>执行结果（Promise）</returns>
    public async Task<object?> ExecuteScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.ExecuteScript: script is empty", _pluginId);
            return null;
        }

        try
        {
            var playerWindow = GetPlayerWindow();
            if (playerWindow?.WebView?.CoreWebView2 == null)
            {
                LogService.Instance.Warn("Plugin:{PluginId}", "WebViewApi.ExecuteScript: WebView not available",
                                         _pluginId);
                return null;
            }

            // 在 UI 线程执行
            string? result = null;
            await Application.Current.Dispatcher.InvokeAsync(
                async () =>
                { result = await playerWindow.WebView.CoreWebView2.ExecuteScriptAsync(script); });

            // 解析结果
            return ParseScriptResult(result);
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Plugin:{PluginId}", "WebViewApi.ExecuteScript error: {ErrorMessage}", _pluginId,
                                      ex.Message);
            throw;
        }
    }

#endregion

#region Private Methods

    /// <summary>
    /// 获取 PlayerWindow 实例
    /// </summary>
    private PlayerWindow? GetPlayerWindow()
    {
        if (_getPlayerWindow != null)
        {
            return _getPlayerWindow();
        }

        // 从 Application 中查找 PlayerWindow
        PlayerWindow? playerWindow = null;
        Application.Current?.Dispatcher.Invoke(() =>
                                               {
                                                   foreach (Window window in Application.Current.Windows)
                                                   {
                                                       if (window is PlayerWindow pw)
                                                       {
                                                           playerWindow = pw;
                                                           break;
                                                       }
                                                   }
                                               });
        return playerWindow;
    }

    /// <summary>
    /// 解析注入时机选项
    /// </summary>
    private static string ParseTiming(object? options)
    {
        if (options == null)
            return "immediate";

        try
        {
            // 尝试从动态对象获取 timing 属性
            if (options is Microsoft.ClearScript.ScriptObject scriptObj)
            {
                var timing = scriptObj.GetProperty("timing");
                if (timing != null)
                    return timing.ToString()?.ToLowerInvariant() ?? "immediate";
            }
            else if (options is System.Dynamic.ExpandoObject expando)
            {
                var dict = (IDictionary<string, object?>)expando;
                if (dict.TryGetValue("timing", out var timing) && timing != null)
                    return timing.ToString()?.ToLowerInvariant() ?? "immediate";
            }
        }
        catch
        {
            // 忽略解析错误
        }

        return "immediate";
    }

    /// <summary>
    /// 根据时机执行脚本
    /// </summary>
    private void ExecuteWithTiming(PlayerWindow playerWindow, string script, string timing)
    {
        Application.Current?.Dispatcher.InvokeAsync(async () =>
                                                    {
                                                        var webView = playerWindow.WebView?.CoreWebView2;
                                                        if (webView == null)
                                                            return;

                                                        switch (timing)
                                                        {
                                                        case "onload":
                                                            // 在页面加载完成后执行
                                                            await webView.AddScriptToExecuteOnDocumentCreatedAsync($@"
                            window.addEventListener('load', function() {{
                                {script}
                            }});
                        ");
                                                            break;

                                                        case "ondomready":
                                                            // 在 DOM 准备好后执行
                                                            await webView.AddScriptToExecuteOnDocumentCreatedAsync($@"
                            if (document.readyState === 'loading') {{
                                document.addEventListener('DOMContentLoaded', function() {{
                                    {script}
                                }});
                            }} else {{
                                {script}
                            }}
                        ");
                                                            break;

                                                        case "immediate":
                                                        default:
                                                            // 立即执行
                                                            await webView.ExecuteScriptAsync(script);
                                                            break;
                                                        }
                                                    });
    }

    /// <summary>
    /// 转义 JavaScript 字符串中的特殊字符
    /// </summary>
    private static string EscapeForJavaScript(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 使用模板字符串，只需要转义反引号和 ${
        return input.Replace("\\", "\\\\").Replace("`", "\\`").Replace("${", "\\${");
    }

    /// <summary>
    /// 解析脚本执行结果
    /// </summary>
    private static object? ParseScriptResult(string? result)
    {
        if (string.IsNullOrEmpty(result))
            return null;

        // WebView2 返回的结果是 JSON 格式
        if (result == "null" || result == "undefined")
            return null;

        // 尝试解析 JSON
        try
        {
            // 如果是字符串，去除外层引号
            if (result.StartsWith("\"") && result.EndsWith("\""))
            {
                return System.Text.Json.JsonSerializer.Deserialize<string>(result);
            }

            // 尝试解析为数字
            if (double.TryParse(result, out var number))
            {
                // 如果是整数，返回整数
                if (number == Math.Floor(number) && number >= int.MinValue && number <= int.MaxValue)
                    return (int)number;
                return number;
            }

            // 尝试解析为布尔值
            if (result == "true")
                return true;
            if (result == "false")
                return false;

            // 尝试解析为 JSON 对象或数组
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            return ConvertJsonElementToObject(doc.RootElement);
        }
        catch
        {
            // 解析失败，返回原始字符串
            return result;
        }
    }

    /// <summary>
    /// 将 JsonElement 转换为 C# 对象
    /// </summary>
    private static object? ConvertJsonElementToObject(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
        case System.Text.Json.JsonValueKind.Null:
        case System.Text.Json.JsonValueKind.Undefined:
            return null;
        case System.Text.Json.JsonValueKind.True:
            return true;
        case System.Text.Json.JsonValueKind.False:
            return false;
        case System.Text.Json.JsonValueKind.Number:
            if (element.TryGetInt32(out var intVal))
                return intVal;
            if (element.TryGetInt64(out var longVal))
                return longVal;
            return element.GetDouble();
        case System.Text.Json.JsonValueKind.String:
            return element.GetString();
        case System.Text.Json.JsonValueKind.Array:
            var list = new System.Collections.Generic.List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                list.Add(ConvertJsonElementToObject(item));
            }
            return list;
        case System.Text.Json.JsonValueKind.Object:
            var dict = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = ConvertJsonElementToObject(prop.Value);
            }
            return dict;
        default:
            return element.ToString();
        }
    }

#endregion
}
}
