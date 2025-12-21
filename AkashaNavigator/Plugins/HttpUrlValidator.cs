using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// HTTP URL 白名单验证器
/// 用于验证插件的 HTTP 请求是否在允许的 URL 列表中
/// </summary>
public class HttpUrlValidator
{
#region Fields

    private readonly string _pluginId;
    private readonly string[] _allowedUrls;
    private readonly Regex[] _patterns;

#endregion

#region Constructor

    /// <summary>
    /// 创建 HTTP URL 验证器实例
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <param name="allowedUrls">允许的 URL 列表（支持通配符 *）</param>
    public HttpUrlValidator(string pluginId, IEnumerable<string>? allowedUrls)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        _allowedUrls = allowedUrls?.ToArray() ?? Array.Empty<string>();
        _patterns = _allowedUrls.Select(CompilePattern).ToArray();
    }

#endregion

#region Public Methods

    /// <summary>
    /// 验证 URL 是否在白名单中
    /// </summary>
    /// <param name="url">要验证的 URL</param>
    /// <exception cref="UnauthorizedAccessException">URL 不在白名单中时抛出</exception>
    public void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty", nameof(url));
        }

        // 如果没有配置白名单，拒绝所有请求
        if (_allowedUrls.Length == 0)
        {
            LogPermissionViolation(url, "no http_allowed_urls configured");
            throw new UnauthorizedAccessException($"Plugin '{_pluginId}' has no http_allowed_urls configured. " +
                                                  $"HTTP requests are not allowed without explicit URL whitelist.");
        }

        // 检查 URL 是否匹配任何白名单模式
        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(url))
            {
                return; // URL 匹配白名单，允许请求
            }
        }

        // URL 不匹配任何白名单模式
        LogPermissionViolation(url, "URL does not match any allowed pattern");
        throw new UnauthorizedAccessException($"Plugin '{_pluginId}' is not allowed to access URL: {url}. " +
                                              $"Allowed URL patterns: [{string.Join(", ", _allowedUrls)}]");
    }

    /// <summary>
    /// 检查 URL 是否在白名单中（不抛出异常）
    /// </summary>
    /// <param name="url">要检查的 URL</param>
    /// <returns>URL 是否被允许</returns>
    public bool IsUrlAllowed(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || _allowedUrls.Length == 0)
        {
            return false;
        }

        return _patterns.Any(pattern => pattern.IsMatch(url));
    }

    /// <summary>
    /// 获取允许的 URL 列表
    /// </summary>
    public IReadOnlyList<string> AllowedUrls => _allowedUrls;

#endregion

#region Private Methods

    /// <summary>
    /// 将通配符模式编译为正则表达式
    /// 支持 * 作为通配符，匹配任意字符
    /// </summary>
    /// <param name="pattern">URL 模式（如 https://api.example.com/*）</param>
    /// <returns>编译后的正则表达式</returns>
    private static Regex CompilePattern(string pattern)
    {
        // 转义正则表达式特殊字符，但保留 * 作为通配符
        var escaped = Regex.Escape(pattern);
        // 将转义后的 \* 替换为 .* 以匹配任意字符
        var regexPattern = "^" + escaped.Replace("\\*", ".*") + "$";
        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>
    /// 记录权限违规日志
    /// </summary>
    private void LogPermissionViolation(string url, string reason)
    {
        Services.LogService.Instance.Warn($"Plugin:{_pluginId}",
                                          $"HTTP permission violation: {reason}. Attempted URL: {url}");
    }

#endregion
}
}
