using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AkashaNavigator.Models.Data;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace AkashaNavigator.Services
{
/// <summary>
/// 字幕服务（单例）
/// 负责拦截、解析和管理 B站视频字幕数据
/// </summary>
public class SubtitleService : ISubtitleService
{
#region Static HttpClient

    /// <summary>
    /// 静态 HttpClient，应用生命周期内复用，避免 socket 耗尽
    /// </summary>
    private static readonly System.Net.Http.HttpClient _httpClient;

    static SubtitleService()
    {
        _httpClient = new System.Net.Http.HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
                                              "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

#endregion

#region Singleton

    private static ISubtitleService? _instance;

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static ISubtitleService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SubtitleService(LogService.Instance);
            }
            return _instance;
        }
        set => _instance = value;
    }

#endregion

#region Fields

    private readonly ILogService _logService;

#endregion

#region Events

    /// <summary>
    /// 字幕数据加载完成事件
    /// </summary>
    public event EventHandler<SubtitleData>? SubtitleLoaded;

    /// <summary>
    /// 当前字幕变化事件
    /// </summary>
    public event EventHandler<SubtitleEntry?>? SubtitleChanged;

    /// <summary>
    /// 字幕数据清除事件
    /// </summary>
    public event EventHandler? SubtitleCleared;

#endregion

#region Fields

    private readonly object _dataLock = new();
    private SubtitleData? _currentSubtitleData;
    private SubtitleEntry? _currentSubtitle;
    private CoreWebView2? _attachedWebView;

    /// <summary>
    /// 缓存的字幕数组，避免重复创建
    /// </summary>
    private SubtitleEntry[]? _cachedSubtitleArray;

#endregion

#region Constructor

    private SubtitleService()
    {
    }

    /// <summary>
    /// DI 容器使用的构造函数
    /// </summary>
    public SubtitleService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        Instance = this; // 设置单例实例供插件系统使用
    }

#endregion

#region JSON Parsing

    /// <summary>
    /// 解析 B站字幕 JSON 数据
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="sourceUrl">来源 URL</param>
    /// <returns>解析后的字幕数据，解析失败返回 null</returns>
    public SubtitleData? ParseSubtitleJson(string json, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var subtitleData = new SubtitleData { SourceUrl = sourceUrl };

            // 尝试获取语言信息（如果存在）
            if (root.TryGetProperty("lan", out var lanElement))
            {
                subtitleData.Language = lanElement.GetString() ?? string.Empty;
            }

            // 解析 body 数组
            if (root.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bodyElement.EnumerateArray())
                {
                    var entry = ParseSubtitleEntry(item);
                    if (entry != null)
                    {
                        subtitleData.Body.Add(entry);
                    }
                }

                // 按开始时间排序
                subtitleData.Body.Sort((a, b) => a.From.CompareTo(b.From));
            }

            return subtitleData;
        }
        catch (JsonException ex)
        {
            _logService.Warn("SubtitleService", "解析字幕 JSON 失败: {ErrorMessage}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "解析字幕时发生异常");
            return null;
        }
    }

    /// <summary>
    /// 解析单条字幕条目
    /// </summary>
    private SubtitleEntry? ParseSubtitleEntry(JsonElement element)
    {
        try
        {
            double from = 0;
            double to = 0;
            string content = string.Empty;

            if (element.TryGetProperty("from", out var fromElement))
            {
                from = fromElement.GetDouble();
            }

            if (element.TryGetProperty("to", out var toElement))
            {
                to = toElement.GetDouble();
            }

            if (element.TryGetProperty("content", out var contentElement))
            {
                content = contentElement.GetString() ?? string.Empty;
            }

            // 验证数据有效性
            if (to <= from || string.IsNullOrEmpty(content))
                return null;

            return new SubtitleEntry { From = from, To = to, Content = content };
        }
        catch
        {
            return null;
        }
    }

#endregion

#region Data Management

    /// <summary>
    /// 获取当前字幕数据
    /// </summary>
    public SubtitleData? GetSubtitleData()
    {
        lock (_dataLock)
        {
            return _currentSubtitleData;
        }
    }

    /// <summary>
    /// 根据时间戳获取字幕
    /// </summary>
    /// <param name="timeInSeconds">时间戳（秒）</param>
    /// <returns>匹配的字幕条目，无匹配返回 null</returns>
    public SubtitleEntry? GetSubtitleAt(double timeInSeconds)
    {
        if (timeInSeconds < 0)
            return null;

        lock (_dataLock)
        {
            if (_currentSubtitleData == null)
                return null;

            // 返回第一个满足 from <= time < to 的字幕
            foreach (var entry in _currentSubtitleData.Body)
            {
                if (entry.ContainsTime(timeInSeconds))
                    return entry;
            }

            return null;
        }
    }

    /// <summary>
    /// 获取所有字幕（返回缓存的不可变数组）
    /// </summary>
    public IReadOnlyList<SubtitleEntry> GetAllSubtitles()
    {
        lock (_dataLock)
        {
            if (_currentSubtitleData == null)
                return Array.Empty<SubtitleEntry>();

            // 返回缓存的数组，避免重复创建
            return _cachedSubtitleArray ??= _currentSubtitleData.Body.ToArray();
        }
    }

    /// <summary>
    /// 清除字幕数据
    /// </summary>
    public void Clear()
    {
        lock (_dataLock)
        {
            _currentSubtitleData = null;
            _currentSubtitle = null;
            _cachedSubtitleArray = null; // 清除缓存
        }

        SubtitleCleared?.Invoke(this, EventArgs.Empty);
        _logService.Debug("SubtitleService", "字幕数据已清除");
    }

    /// <summary>
    /// 更新当前播放时间，检测字幕变化
    /// </summary>
    /// <param name="timeInSeconds">当前播放时间（秒）</param>
    public void UpdateCurrentTime(double timeInSeconds)
    {
        SubtitleEntry? newSubtitle = GetSubtitleAt(timeInSeconds);

        lock (_dataLock)
        {
            // 检查字幕是否变化
            bool changed = false;
            if (_currentSubtitle == null && newSubtitle != null)
            {
                changed = true;
            }
            else if (_currentSubtitle != null && newSubtitle == null)
            {
                changed = true;
            }
            else if (_currentSubtitle != null && newSubtitle != null)
            {
                // 比较内容是否相同
                changed = _currentSubtitle.From != newSubtitle.From || _currentSubtitle.To != newSubtitle.To ||
                          _currentSubtitle.Content != newSubtitle.Content;
            }

            if (changed)
            {
                _currentSubtitle = newSubtitle;
                if (newSubtitle != null)
                {
                    _logService.Debug("SubtitleService", "字幕变化: [{From:F1}s-{To:F1}s] {Content}",
                                              newSubtitle.From, newSubtitle.To, newSubtitle.Content);
                }
                else
                {
                    _logService.Debug("SubtitleService", "字幕变化: 无字幕");
                }
            }
            else
            {
                return; // 没有变化，不触发事件
            }
        }

        SubtitleChanged?.Invoke(this, newSubtitle);
    }

    /// <summary>
    /// 设置字幕数据（内部使用）
    /// </summary>
    internal void SetSubtitleData(SubtitleData data)
    {
        lock (_dataLock)
        {
            _currentSubtitleData = data;
            _currentSubtitle = null;
            _cachedSubtitleArray = null; // 使缓存失效
        }

        _logService.Info("SubtitleService", "字幕数据已加载，共 {SubtitleCount} 条字幕，语言: {Language}",
                                 data.Body.Count, data.Language);

        // 输出前几条字幕作为预览
        if (data.Body.Count > 0)
        {
            var previewCount = Math.Min(3, data.Body.Count);
            for (int i = 0; i < previewCount; i++)
            {
                var entry = data.Body[i];
                _logService.Debug("SubtitleService", "  字幕预览[{Index}]: [{From:F1}s-{To:F1}s] {Content}", i,
                                          entry.From, entry.To, entry.Content);
            }
            if (data.Body.Count > 3)
            {
                _logService.Debug("SubtitleService", "  ... 还有 {RemainingCount} 条字幕", data.Body.Count - 3);
            }
        }

        SubtitleLoaded?.Invoke(this, data);
    }

#endregion

#region WebView2 Integration

    /// <summary>
    /// 附加到 WebView2
    /// </summary>
    /// <param name="webView">WebView2 核心对象</param>
    public void AttachToWebView(CoreWebView2 webView)
    {
        if (webView == null)
            throw new ArgumentNullException(nameof(webView));

        // 先分离之前的 WebView
        if (_attachedWebView != null)
        {
            DetachFromWebView(_attachedWebView);
        }

        _attachedWebView = webView;

        // 添加被动拦截：监听 player/v2 API 请求的响应
        webView.WebResourceResponseReceived += OnWebResourceResponseReceived;

        _logService.Debug("SubtitleService", "已附加到 WebView2");
    }

    /// <summary>
    /// 被动拦截 B站 player/v2 API 响应
    /// </summary>
    private async void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        try
        {
            var uri = e.Request.Uri;

            // 只拦截 B站 player/wbi/v2 或 player/v2 API 请求（字幕信息 API）
            // 排除 online/total 等其他 player 相关 API
            if (!uri.Contains("api.bilibili.com/x/player/wbi/v2") && !uri.Contains("api.bilibili.com/x/player/v2"))
                return;

            // 必须包含 cid 参数（B站页面自己的请求才有 cid）
            if (!uri.Contains("cid="))
                return;

            _logService.Info("SubtitleService", "★ 拦截到 player/v2 API: {ApiUrl}...",
                                     uri.Substring(0, Math.Min(100, uri.Length)));

            var response = e.Response;
            if (response == null || response.StatusCode != 200)
                return;

            // 读取响应内容
            var stream = await response.GetContentAsync();
            if (stream == null)
                return;

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(content))
                return;

            // 解析 JSON 获取字幕 URL
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (!root.TryGetProperty("code", out var codeEl) || codeEl.GetInt32() != 0)
                return;

            if (!root.TryGetProperty("data", out var dataEl))
                return;

            // 获取 cid
            var cid = dataEl.TryGetProperty("cid", out var cidEl) ? cidEl.GetInt64().ToString() : "";
            _logService.Debug("SubtitleService", "拦截到 cid: {Cid}", cid);

            // 获取字幕列表
            if (!dataEl.TryGetProperty("subtitle", out var subtitleEl))
                return;

            if (!subtitleEl.TryGetProperty("subtitles", out var subtitlesEl) ||
                subtitlesEl.ValueKind != JsonValueKind.Array)
                return;

            var subtitles = subtitlesEl.EnumerateArray().ToList();
            if (subtitles.Count == 0)
                return;

            _logService.Info("SubtitleService", "被动拦截到 {SubtitleCount} 个字幕", subtitles.Count);

            // 优先选择中文字幕（ai-zh 优先，稳定性更好）
            JsonElement? selectedSubtitle = null;
            JsonElement? fallbackSubtitle = null;
            foreach (var sub in subtitles)
            {
                if (sub.TryGetProperty("lan", out var lanEl))
                {
                    var lan = lanEl.GetString() ?? "";
                    // ai-zh 最优先
                    if (lan == "ai-zh")
                    {
                        selectedSubtitle = sub;
                        break;
                    }
                    // 其他中文字幕作为备选
                    if (fallbackSubtitle == null && (lan == "zh-CN" || lan.StartsWith("zh")))
                    {
                        fallbackSubtitle = sub;
                    }
                }
            }
            selectedSubtitle ??= fallbackSubtitle ?? subtitles.FirstOrDefault();

            if (selectedSubtitle == null)
                return;

            // 获取字幕 URL
            if (!selectedSubtitle.Value.TryGetProperty("subtitle_url", out var urlEl))
                return;

            var subtitleUrl = urlEl.GetString() ?? "";
            if (string.IsNullOrEmpty(subtitleUrl))
                return;

            // 确保 URL 有协议
            if (subtitleUrl.StartsWith("//"))
                subtitleUrl = "https:" + subtitleUrl;

            var language = selectedSubtitle.Value.TryGetProperty("lan", out var langEl) ? langEl.GetString() ?? "" : "";

            _logService.Info("SubtitleService", "被动拦截字幕 URL: {SubtitleUrl}...",
                                     subtitleUrl.Substring(0, Math.Min(80, subtitleUrl.Length)));

            // 请求字幕内容
            await FetchSubtitleContentAsync(subtitleUrl, language);
        }
        catch (Exception ex)
        {
            _logService.Debug("SubtitleService", "拦截 player/v2 响应时出错: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// 请求字幕内容
    /// </summary>
    private async Task FetchSubtitleContentAsync(string url, string language)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);

            if (string.IsNullOrEmpty(response))
            {
                _logService.Warn("SubtitleService", "字幕响应为空");
                return;
            }

            // 解析字幕 JSON
            var subtitleData = ParseSubtitleJson(response, url);
            if (subtitleData != null)
            {
                subtitleData.Language = language;
                if (subtitleData.Body.Count > 0)
                {
                    SetSubtitleData(subtitleData);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "请求字幕内容失败");
        }
    }

    /// <summary>
    /// 主动请求获取 B站字幕数据（通过 API）
    /// 注意：现在主要依赖被动拦截，此方法作为备用
    /// </summary>
    public async Task RequestSubtitleAsync()
    {
        if (_attachedWebView == null)
        {
            _logService.Warn("SubtitleService", "WebView2 未附加，无法请求字幕");
            return;
        }

        // 检查是否是 B站视频页面
        var currentUrl = _attachedWebView.Source;
        if (string.IsNullOrEmpty(currentUrl) || !currentUrl.Contains("bilibili.com"))
        {
            _logService.Debug("SubtitleService", "非 B站页面，跳过字幕请求");
            return;
        }

        // 如果已经有字幕数据（被动拦截获取的），跳过主动请求
        if (GetSubtitleData() != null && GetSubtitleData()!.Body.Count > 0)
        {
            _logService.Debug("SubtitleService", "已有字幕数据（被动拦截），跳过主动请求");
            return;
        }

        _logService.Debug("SubtitleService", "开始主动请求 B站字幕数据...");

        // 注入 JS 获取字幕
        var script = @"
(async function() {
    try {
        // 获取视频信息
        let bvid = window.__INITIAL_STATE__?.bvid || 
                   window.__INITIAL_STATE__?.videoData?.bvid;
        let aid = window.__INITIAL_STATE__?.aid ||
                  window.__INITIAL_STATE__?.videoData?.aid;

        console.log('[AkashaNavigator] 视频信息: bvid=' + bvid + ', aid=' + aid);

        if (!bvid && !aid) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'subtitle_error',
                error: '无法获取视频 bvid/aid'
            }));
            return;
        }

        // 请求 API（不传 cid，API 会返回当前分P的正确字幕 URL）
        let apiUrl = 'https://api.bilibili.com/x/player/v2?';
        if (bvid) {
            apiUrl += 'bvid=' + bvid;
        } else if (aid) {
            apiUrl += 'aid=' + aid;
        }

        console.log('[AkashaNavigator] 请求字幕 API: ' + apiUrl);

        const response = await fetch(apiUrl, { 
            credentials: 'include',
            headers: {
                'Referer': 'https://www.bilibili.com/',
                'Origin': 'https://www.bilibili.com'
            }
        });
        
        const contentType = response.headers.get('content-type') || '';
        if (!contentType.includes('application/json')) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'subtitle_error',
                error: 'API 返回非 JSON 响应'
            }));
            return;
        }
        
        const data = await response.json();
        if (data.code !== 0) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'subtitle_error',
                error: 'API 返回错误: ' + (data.message || data.code)
            }));
            return;
        }

        const cid = data?.data?.cid;
        console.log('[AkashaNavigator] API 返回 cid: ' + cid);

        const subtitles = data?.data?.subtitle?.subtitles || [];
        console.log('[AkashaNavigator] 找到 ' + subtitles.length + ' 个字幕');

        if (subtitles.length === 0) {
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'subtitle_info',
                message: '该视频没有字幕'
            }));
            return;
        }

        // 优先选择中文字幕（ai-zh 优先，稳定性更好）
        let selectedSubtitle = subtitles.find(s => s.lan === 'ai-zh') || 
                               subtitles.find(s => s.lan === 'zh-CN') || 
                               subtitles.find(s => s.lan.startsWith('zh')) ||
                               subtitles[0];
        let subtitleUrl = selectedSubtitle.subtitle_url;
        
        // 确保 URL 有协议
        if (subtitleUrl.startsWith('//')) {
            subtitleUrl = 'https:' + subtitleUrl;
        }

        console.log('[AkashaNavigator] 选择字幕: ' + selectedSubtitle.lan + ', URL: ' + subtitleUrl);

        // 发送字幕 URL 给 C#，由 C# 端请求（避免 CORS 问题）
        window.chrome.webview.postMessage(JSON.stringify({
            type: 'subtitle_url',
            language: selectedSubtitle.lan,
            url: subtitleUrl
        }));

    } catch (error) {
        console.error('[AkashaNavigator] 获取字幕失败:', error);
        window.chrome.webview.postMessage(JSON.stringify({
            type: 'subtitle_error',
            error: error.message || '未知错误'
        }));
    }
})();
";

        try
        {
            await _attachedWebView.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "执行字幕获取脚本失败");
        }
    }

    /// <summary>
    /// 处理从 WebView2 收到的字幕数据消息
    /// </summary>
    /// <param name="jsonMessage">JSON 格式的消息</param>
    public void HandleSubtitleMessage(string jsonMessage)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonMessage);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();

            switch (type)
            {
            case "subtitle_url":
                // 收到字幕 URL，由 C# 端请求字幕内容（避免 CORS）
                _ = HandleSubtitleUrlAsync(root);
                break;

            case "subtitle_data":
                HandleSubtitleData(root);
                break;

            case "subtitle_error":
                if (root.TryGetProperty("error", out var errorElement))
                {
                    _logService.Warn("SubtitleService", "字幕获取失败: {ErrorMessage}",
                                             errorElement.GetString());
                }
                break;

            case "subtitle_info":
                if (root.TryGetProperty("message", out var msgElement))
                {
                    _logService.Info("SubtitleService", msgElement.GetString() ?? "");
                }
                break;
            }
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "处理字幕消息失败");
        }
    }

    private async Task HandleSubtitleUrlAsync(JsonElement root)
    {
        try
        {
            var language = root.TryGetProperty("language", out var langEl) ? langEl.GetString() ?? "" : "";
            var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(url))
            {
                _logService.Debug("SubtitleService", "字幕 URL 为空，跳过请求");
                return;
            }

            _logService.Debug("SubtitleService", "开始请求字幕内容: {SubtitleUrl}...",
                                      url.Substring(0, Math.Min(80, url.Length)));

            var response = await _httpClient.GetStringAsync(url);

            if (string.IsNullOrEmpty(response))
            {
                _logService.Warn("SubtitleService", "字幕响应为空");
                return;
            }

            _logService.Debug("SubtitleService", "字幕响应长度: {ResponseLength}", response.Length);

            // 解析字幕 JSON
            var subtitleData = ParseSubtitleJson(response, url);
            if (subtitleData != null)
            {
                subtitleData.Language = language;
                if (subtitleData.Body.Count > 0)
                {
                    SetSubtitleData(subtitleData);
                }
                else
                {
                    _logService.Warn("SubtitleService", "解析后没有有效的字幕条目");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "请求字幕内容失败");
        }
    }

    private void HandleSubtitleData(JsonElement root)
    {
        try
        {
            var language = root.TryGetProperty("language", out var langEl) ? langEl.GetString() ?? "" : "";
            var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "";

            if (!root.TryGetProperty("data", out var dataElement))
            {
                _logService.Warn("SubtitleService", "字幕消息中没有 data 字段");
                return;
            }

            // 解析字幕数据
            var subtitleData = new SubtitleData { SourceUrl = url, Language = language };

            // 解析 body 数组
            if (dataElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in bodyElement.EnumerateArray())
                {
                    double from = 0, to = 0;
                    string content = "";

                    if (item.TryGetProperty("from", out var fromEl))
                        from = fromEl.GetDouble();
                    if (item.TryGetProperty("to", out var toEl))
                        to = toEl.GetDouble();
                    if (item.TryGetProperty("content", out var contentEl))
                        content = contentEl.GetString() ?? "";

                    if (to > from && !string.IsNullOrEmpty(content))
                    {
                        subtitleData.Body.Add(new SubtitleEntry { From = from, To = to, Content = content });
                    }
                }

                subtitleData.Body.Sort((a, b) => a.From.CompareTo(b.From));
            }

            if (subtitleData.Body.Count > 0)
            {
                SetSubtitleData(subtitleData);
            }
            else
            {
                _logService.Warn("SubtitleService", "解析后没有有效的字幕条目");
            }
        }
        catch (Exception ex)
        {
            _logService.Error("SubtitleService", ex, "解析字幕数据失败");
        }
    }

    /// <summary>
    /// 从 WebView2 分离
    /// </summary>
    /// <param name="webView">WebView2 核心对象</param>
    public void DetachFromWebView(CoreWebView2 webView)
    {
        if (webView == null)
            return;

        // 移除事件监听
        webView.WebResourceResponseReceived -= OnWebResourceResponseReceived;

        if (_attachedWebView == webView)
        {
            _attachedWebView = null;
            // 清理字幕数据，释放内存
            Clear();
        }

        _logService.Debug("SubtitleService", "已从 WebView2 分离并清理数据");
    }

#endregion
}
}
