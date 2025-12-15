using System;
using System.Threading.Tasks;
using FloatWebPlayer.Views;
using Microsoft.Web.WebView2.Core;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 播放器控制 API
    /// 提供视频播放控制功能
    /// 需要 "player" 权限
    /// </summary>
    public class PlayerApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly Func<PlayerWindow?> _getWindow;
        private EventApi? _eventApi;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建播放器 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public PlayerApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _getWindow = () => null;
        }

        /// <summary>
        /// 创建播放器 API 实例（带窗口引用）
        /// </summary>
        /// <param name="context">插件上下文</param>
        /// <param name="getWindow">获取 PlayerWindow 的委托</param>
        public PlayerApi(PluginContext context, Func<PlayerWindow?> getWindow)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _getWindow = getWindow ?? throw new ArgumentNullException(nameof(getWindow));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 设置 EventApi 引用（用于触发事件）
        /// </summary>
        internal void SetEventApi(EventApi? eventApi)
        {
            _eventApi = eventApi;
        }


        /// <summary>
        /// 获取 WebView2 CoreWebView2 实例
        /// </summary>
        private CoreWebView2? GetWebView()
        {
            var window = _getWindow();
            if (window == null)
                return null;

            // 需要在 UI 线程访问 WebView
            return window.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 通过反射或公开属性获取 WebView
                    var webViewField = window.GetType().GetField("WebView", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (webViewField != null)
                    {
                        var webView = webViewField.GetValue(window) as Microsoft.Web.WebView2.Wpf.WebView2;
                        return webView?.CoreWebView2;
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// 执行 JavaScript 脚本并返回结果
        /// </summary>
        private async Task<string?> ExecuteScriptAsync(string script)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "PlayerApi: PlayerWindow not available");
                return null;
            }

            try
            {
                return await window.Dispatcher.InvokeAsync(async () =>
                {
                    var webView = GetWebViewFromWindow(window);
                    if (webView?.CoreWebView2 == null)
                    {
                        Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                            "PlayerApi: WebView2 not initialized");
                        return null;
                    }

                    return await webView.CoreWebView2.ExecuteScriptAsync(script);
                }).Result;
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                    $"PlayerApi: Script execution failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从 PlayerWindow 获取 WebView2 控件
        /// </summary>
        private Microsoft.Web.WebView2.Wpf.WebView2? GetWebViewFromWindow(PlayerWindow window)
        {
            // PlayerWindow.xaml 中 WebView 是 x:Name="WebView"
            // 通过 FindName 或直接访问
            try
            {
                return window.FindName("WebView") as Microsoft.Web.WebView2.Wpf.WebView2;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 执行 JavaScript 脚本（无返回值）
        /// </summary>
        private void ExecuteScript(string script)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "PlayerApi: PlayerWindow not available");
                return;
            }

            window.Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var webView = GetWebViewFromWindow(window);
                    if (webView?.CoreWebView2 == null)
                    {
                        Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                            "PlayerApi: WebView2 not initialized");
                        return;
                    }

                    await webView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch (Exception ex)
                {
                    Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                        $"PlayerApi: Script execution failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 执行 JavaScript 脚本并同步返回结果
        /// </summary>
        private string? ExecuteScriptSync(string script)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "PlayerApi: PlayerWindow not available");
                return null;
            }

            try
            {
                return window.Dispatcher.Invoke(() =>
                {
                    var webView = GetWebViewFromWindow(window);
                    if (webView?.CoreWebView2 == null)
                    {
                        Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                            "PlayerApi: WebView2 not initialized");
                        return null;
                    }

                    // 使用 Task.Run 和 GetAwaiter().GetResult() 来同步等待
                    var task = webView.CoreWebView2.ExecuteScriptAsync(script);
                    return task.GetAwaiter().GetResult();
                });
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                    $"PlayerApi: Script execution failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            // PlayerApi 没有事件监听器需要清理
            // 清空 EventApi 引用
            _eventApi = null;
            
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "PlayerApi: cleaned up");
        }

        #endregion

        #region Playback Control

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "PlayerApi.Play()");
            
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        video.play();
                        return true;
                    }
                    return false;
                })();
            ";

            ExecuteScript(script);
            
            // 触发播放状态变化事件
            _eventApi?.Emit(EventApi.PlayStateChanged, new { playing = true });
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void Pause()
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "PlayerApi.Pause()");
            
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        video.pause();
                        return true;
                    }
                    return false;
                })();
            ";

            ExecuteScript(script);
            
            // 触发播放状态变化事件
            _eventApi?.Emit(EventApi.PlayStateChanged, new { playing = false });
        }

        /// <summary>
        /// 切换播放/暂停状态
        /// </summary>
        public void TogglePlay()
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "PlayerApi.TogglePlay()");
            
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        if (video.paused) {
                            video.play();
                            return 'playing';
                        } else {
                            video.pause();
                            return 'paused';
                        }
                    }
                    return 'no-video';
                })();
            ";

            var result = ExecuteScriptSync(script);
            
            // 根据结果触发事件
            if (result != null)
            {
                var isPlaying = result.Contains("playing");
                _eventApi?.Emit(EventApi.PlayStateChanged, new { playing = isPlaying });
            }
        }

        #endregion


        #region Seek Control

        /// <summary>
        /// 跳转到指定时间
        /// </summary>
        /// <param name="seconds">目标时间（秒）</param>
        public void Seek(double seconds)
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"PlayerApi.Seek({seconds})");
            
            // 确保时间非负
            var targetTime = Math.Max(0, seconds);
            
            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.currentTime = {targetTime.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        return video.currentTime;
                    }}
                    return -1;
                }})();
            ";

            ExecuteScript(script);
        }

        /// <summary>
        /// 相对跳转
        /// </summary>
        /// <param name="seconds">偏移量（秒，正数前进，负数后退）</param>
        public void SeekRelative(double seconds)
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"PlayerApi.SeekRelative({seconds})");
            
            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.currentTime += {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        return video.currentTime;
                    }}
                    return -1;
                }})();
            ";

            ExecuteScript(script);
        }

        /// <summary>
        /// 获取当前播放时间
        /// </summary>
        /// <returns>当前时间（秒）</returns>
        public double GetCurrentTime()
        {
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        return video.currentTime;
                    }
                    return 0;
                })();
            ";

            var result = ExecuteScriptSync(script);
            if (result != null && double.TryParse(result, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double time))
            {
                return time;
            }
            return 0;
        }

        /// <summary>
        /// 获取视频总时长
        /// </summary>
        /// <returns>总时长（秒）</returns>
        public double GetDuration()
        {
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video && !isNaN(video.duration)) {
                        return video.duration;
                    }
                    return 0;
                })();
            ";

            var result = ExecuteScriptSync(script);
            if (result != null && double.TryParse(result, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
            return 0;
        }

        #endregion

        #region Playback Rate

        /// <summary>
        /// 设置播放速度
        /// </summary>
        /// <param name="rate">播放速度（1.0 为正常速度）</param>
        public void SetPlaybackRate(double rate)
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"PlayerApi.SetPlaybackRate({rate})");
            
            // 钳制播放速度到合理范围
            var clampedRate = Math.Clamp(rate, 0.25, 4.0);
            
            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.playbackRate = {clampedRate.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        return video.playbackRate;
                    }}
                    return -1;
                }})();
            ";

            ExecuteScript(script);
        }

        /// <summary>
        /// 获取当前播放速度
        /// </summary>
        /// <returns>播放速度</returns>
        public double GetPlaybackRate()
        {
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        return video.playbackRate;
                    }
                    return 1.0;
                })();
            ";

            var result = ExecuteScriptSync(script);
            if (result != null && double.TryParse(result, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double rate))
            {
                return rate;
            }
            return 1.0;
        }

        #endregion

        #region Volume Control

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量（0.0 到 1.0）</param>
        public void SetVolume(double volume)
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"PlayerApi.SetVolume({volume})");
            
            // 钳制音量到有效范围
            var clampedVolume = Math.Clamp(volume, 0.0, 1.0);
            
            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.volume = {clampedVolume.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        return video.volume;
                    }}
                    return -1;
                }})();
            ";

            ExecuteScript(script);
        }

        /// <summary>
        /// 获取当前音量
        /// </summary>
        /// <returns>音量（0.0 到 1.0）</returns>
        public double GetVolume()
        {
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        return video.volume;
                    }
                    return 1.0;
                })();
            ";

            var result = ExecuteScriptSync(script);
            if (result != null && double.TryParse(result, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double volume))
            {
                return volume;
            }
            return 1.0;
        }

        /// <summary>
        /// 设置静音状态
        /// </summary>
        /// <param name="muted">是否静音</param>
        public void SetMuted(bool muted)
        {
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"PlayerApi.SetMuted({muted})");
            
            string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.muted = {(muted ? "true" : "false")};
                        return video.muted;
                    }}
                    return false;
                }})();
            ";

            ExecuteScript(script);
        }

        /// <summary>
        /// 获取静音状态
        /// </summary>
        /// <returns>是否静音</returns>
        public bool IsMuted()
        {
            const string script = @"
                (function() {
                    var video = document.querySelector('video');
                    if (video) {
                        return video.muted;
                    }
                    return false;
                })();
            ";

            var result = ExecuteScriptSync(script);
            if (result != null)
            {
                return result.Trim().ToLower() == "true";
            }
            return false;
        }

        #endregion
    }
}
