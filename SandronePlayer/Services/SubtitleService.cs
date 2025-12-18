using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SandronePlayer.Models;
using Microsoft.Web.WebView2.Core;

namespace SandronePlayer.Services
{
    /// <summary>
    /// 字幕服务（单例）
    /// 负责拦截、解析和管理 B站视频字幕数据
    /// </summary>
    public class SubtitleService
    {
        #region Singleton

        private static SubtitleService? _instance;
        private static readonly object _singletonLock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static SubtitleService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLock)
                    {
                        _instance ??= new SubtitleService();
                    }
                }
                return _instance;
            }
        }

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

        #endregion

        #region Constructor

        private SubtitleService()
        {
        }

        /// <summary>
        /// 仅用于测试的内部构造函数
        /// </summary>
        internal SubtitleService(bool forTesting)
        {
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

                var subtitleData = new SubtitleData
                {
                    SourceUrl = sourceUrl
                };

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
                LogService.Instance.Warn("SubtitleService", $"解析字幕 JSON 失败: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubtitleService", "解析字幕时发生异常", ex);
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

                return new SubtitleEntry
                {
                    From = from,
                    To = to,
                    Content = content
                };
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
        /// 获取所有字幕（返回不可变副本）
        /// </summary>
        public IReadOnlyList<SubtitleEntry> GetAllSubtitles()
        {
            lock (_dataLock)
            {
                if (_currentSubtitleData == null)
                    return Array.Empty<SubtitleEntry>();

                // 返回副本以防止外部修改
                return _currentSubtitleData.Body.ToArray();
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
            }

            SubtitleCleared?.Invoke(this, EventArgs.Empty);
            LogService.Instance.Debug("SubtitleService", "字幕数据已清除");
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
                    changed = _currentSubtitle.From != newSubtitle.From ||
                              _currentSubtitle.To != newSubtitle.To ||
                              _currentSubtitle.Content != newSubtitle.Content;
                }

                if (changed)
                {
                    _currentSubtitle = newSubtitle;
                    if (newSubtitle != null)
                    {
                        LogService.Instance.Debug("SubtitleService", $"字幕变化: [{newSubtitle.From:F1}s-{newSubtitle.To:F1}s] {newSubtitle.Content}");
                    }
                    else
                    {
                        LogService.Instance.Debug("SubtitleService", "字幕变化: 无字幕");
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
            }

            LogService.Instance.Info("SubtitleService", $"字幕数据已加载，共 {data.Body.Count} 条字幕，语言: {data.Language}");
            
            // 输出前几条字幕作为预览
            if (data.Body.Count > 0)
            {
                var previewCount = Math.Min(3, data.Body.Count);
                for (int i = 0; i < previewCount; i++)
                {
                    var entry = data.Body[i];
                    LogService.Instance.Debug("SubtitleService", $"  字幕预览[{i}]: [{entry.From:F1}s-{entry.To:F1}s] {entry.Content}");
                }
                if (data.Body.Count > 3)
                {
                    LogService.Instance.Debug("SubtitleService", $"  ... 还有 {data.Body.Count - 3} 条字幕");
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
            
            LogService.Instance.Debug("SubtitleService", "已附加到 WebView2");
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
                if (!uri.Contains("api.bilibili.com/x/player/wbi/v2") && 
                    !uri.Contains("api.bilibili.com/x/player/v2"))
                    return;

                // 必须包含 cid 参数（B站页面自己的请求才有 cid）
                if (!uri.Contains("cid="))
                    return;

                LogService.Instance.Info("SubtitleService", $"★ 拦截到 player/v2 API: {uri.Substring(0, Math.Min(100, uri.Length))}...");

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
                LogService.Instance.Debug("SubtitleService", $"拦截到 cid: {cid}");

                // 获取字幕列表
                if (!dataEl.TryGetProperty("subtitle", out var subtitleEl))
                    return;

                if (!subtitleEl.TryGetProperty("subtitles", out var subtitlesEl) || subtitlesEl.ValueKind != JsonValueKind.Array)
                    return;

                var subtitles = subtitlesEl.EnumerateArray().ToList();
                if (subtitles.Count == 0)
                    return;

                LogService.Instance.Info("SubtitleService", $"被动拦截到 {subtitles.Count} 个字幕");

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
                
                LogService.Instance.Info("SubtitleService", $"被动拦截字幕 URL: {subtitleUrl.Substring(0, Math.Min(80, subtitleUrl.Length))}...");

                // 请求字幕内容
                await FetchSubtitleContentAsync(subtitleUrl, language);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("SubtitleService", $"拦截 player/v2 响应时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 请求字幕内容
        /// </summary>
        private async Task FetchSubtitleContentAsync(string url, string language)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com/");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await httpClient.GetStringAsync(url);
                
                if (string.IsNullOrEmpty(response))
                {
                    LogService.Instance.Warn("SubtitleService", "字幕响应为空");
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
                LogService.Instance.Error("SubtitleService", $"请求字幕内容失败: {ex.Message}", ex);
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
                LogService.Instance.Warn("SubtitleService", "WebView2 未附加，无法请求字幕");
                return;
            }

            // 检查是否是 B站视频页面
            var currentUrl = _attachedWebView.Source;
            if (string.IsNullOrEmpty(currentUrl) || !currentUrl.Contains("bilibili.com"))
            {
                LogService.Instance.Debug("SubtitleService", "非 B站页面，跳过字幕请求");
                return;
            }

            // 如果已经有字幕数据（被动拦截获取的），跳过主动请求
            if (GetSubtitleData() != null && GetSubtitleData()!.Body.Count > 0)
            {
                LogService.Instance.Debug("SubtitleService", "已有字幕数据（被动拦截），跳过主动请求");
                return;
            }

            LogService.Instance.Debug("SubtitleService", "开始主动请求 B站字幕数据...");

            // 注入 JS 获取字幕
            var script = @"
(async function() {
    try {
        // 获取视频信息
        let bvid = window.__INITIAL_STATE__?.bvid || 
                   window.__INITIAL_STATE__?.videoData?.bvid;
        let aid = window.__INITIAL_STATE__?.aid ||
                  window.__INITIAL_STATE__?.videoData?.aid;

        console.log('[SandronePlayer] 视频信息: bvid=' + bvid + ', aid=' + aid);

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

        console.log('[SandronePlayer] 请求字幕 API: ' + apiUrl);

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
        console.log('[SandronePlayer] API 返回 cid: ' + cid);

        const subtitles = data?.data?.subtitle?.subtitles || [];
        console.log('[SandronePlayer] 找到 ' + subtitles.length + ' 个字幕');

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

        console.log('[SandronePlayer] 选择字幕: ' + selectedSubtitle.lan + ', URL: ' + subtitleUrl);

        // 发送字幕 URL 给 C#，由 C# 端请求（避免 CORS 问题）
        window.chrome.webview.postMessage(JSON.stringify({
            type: 'subtitle_url',
            language: selectedSubtitle.lan,
            url: subtitleUrl
        }));

    } catch (error) {
        console.error('[SandronePlayer] 获取字幕失败:', error);
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
                LogService.Instance.Error("SubtitleService", "执行字幕获取脚本失败", ex);
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
                            LogService.Instance.Warn("SubtitleService", $"字幕获取失败: {errorElement.GetString()}");
                        }
                        break;

                    case "subtitle_info":
                        if (root.TryGetProperty("message", out var msgElement))
                        {
                            LogService.Instance.Info("SubtitleService", msgElement.GetString() ?? "");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubtitleService", "处理字幕消息失败", ex);
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
                    LogService.Instance.Debug("SubtitleService", "字幕 URL 为空，跳过请求");
                    return;
                }

                LogService.Instance.Debug("SubtitleService", $"开始请求字幕内容: {url.Substring(0, Math.Min(80, url.Length))}...");

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com/");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await httpClient.GetStringAsync(url);
                
                if (string.IsNullOrEmpty(response))
                {
                    LogService.Instance.Warn("SubtitleService", "字幕响应为空");
                    return;
                }

                LogService.Instance.Debug("SubtitleService", $"字幕响应长度: {response.Length}");

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
                        LogService.Instance.Warn("SubtitleService", "解析后没有有效的字幕条目");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubtitleService", $"请求字幕内容失败: {ex.Message}", ex);
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
                    LogService.Instance.Warn("SubtitleService", "字幕消息中没有 data 字段");
                    return;
                }

                // 解析字幕数据
                var subtitleData = new SubtitleData
                {
                    SourceUrl = url,
                    Language = language
                };

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
                            subtitleData.Body.Add(new SubtitleEntry
                            {
                                From = from,
                                To = to,
                                Content = content
                            });
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
                    LogService.Instance.Warn("SubtitleService", "解析后没有有效的字幕条目");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubtitleService", "解析字幕数据失败", ex);
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
            }

            LogService.Instance.Debug("SubtitleService", "已从 WebView2 分离");
        }

        #endregion
    }
}
