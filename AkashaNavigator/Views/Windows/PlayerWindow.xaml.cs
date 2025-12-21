using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;
using Microsoft.Web.WebView2.Core;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// PlayerWindow - 播放器主窗口
/// </summary>
public partial class PlayerWindow : Window
{
#region Events

    /// <summary>
    /// URL 变化事件
    /// </summary>
    public event EventHandler<string>? UrlChanged;

    /// <summary>
    /// 导航状态变化事件
    /// </summary>
    public event EventHandler? NavigationStateChanged;

#endregion

#region Fields

    /// <summary>
    /// 是否最大化
    /// </summary>
    private bool _isMaximized;

    /// <summary>
    /// 最大化前的窗口边界
    /// </summary>
    private Rect _restoreBounds;

    /// <summary>
    /// 当前配置引用
    /// </summary>
    private AppConfig _config;

    /// <summary>
    /// 窗口行为辅助类（边缘吸附、透明度控制）
    /// </summary>
    private WindowBehaviorHelper _windowBehavior = null!;

    /// <summary>
    /// 鼠标检测前保存的透明度
    /// </summary>
    private double _opacityBeforeCursorDetection = 1.0;

    /// <summary>
    /// 鼠标检测配置的最低透明度
    /// </summary>
    private double _cursorDetectionMinOpacity = 0.3;

    /// <summary>
    /// 是否因鼠标检测而降低了透明度
    /// </summary>
    private bool _isOpacityReducedByCursorDetection;

    /// <summary>
    /// 视频时间同步定时器
    /// </summary>
    private DispatcherTimer? _videoTimeSyncTimer;

#endregion

#region Constructor

    public PlayerWindow()
    {
        InitializeComponent();
        _config = ConfigService.Instance.Config;
        InitializeWindowPosition();
        InitializeWindowBehavior();
        InitializeWebView();

        // 订阅 Profile 切换事件
        ProfileManager.Instance.ProfileChanged += OnProfileChanged;

        // 初始化鼠标检测（如果当前 Profile 启用了）
        InitializeCursorDetection();

        // 窗口关闭时清理
        Closing += PlayerWindow_Closing;

        // 窗口关闭后退出应用
        Closed += (s, e) =>
        { System.Windows.Application.Current.Shutdown(); };
    }

#endregion

#region Initialization

    /// <summary>
    /// 初始化窗口位置和大小
    /// 从 WindowStateService 加载上次保存的状态
    /// </summary>
    private void InitializeWindowPosition()
    {
        var state = WindowStateService.Instance.Load();

        // 应用保存的位置和大小
        Left = state.Left;
        Top = state.Top;
        Width = Math.Max(state.Width, AppConstants.MinWindowWidth);
        Height = Math.Max(state.Height, AppConstants.MinWindowHeight);

        // 确保窗口在屏幕范围内（允许延伸到任务栏区域）
        var workArea = SystemParameters.WorkArea;
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        if (Left < workArea.Left)
            Left = workArea.Left;
        if (Top < workArea.Top)
            Top = workArea.Top;
        if (Left + Width > workArea.Right)
            Left = workArea.Right - Width;
        if (Top + Height > screenHeight)
            Top = screenHeight - Height; // 使用屏幕高度而非工作区
    }

    /// <summary>
    /// 初始化窗口行为辅助类
    /// </summary>
    private void InitializeWindowBehavior()
    {
        var state = WindowStateService.Instance.Load();
        _windowBehavior = new WindowBehaviorHelper(this, _config, state.Opacity);
    }

    /// <summary>
    /// 保存窗口状态
    /// </summary>
    private void SaveWindowState()
    {
        var state = WindowStateService.Instance.Load();
        state.Left = Left;
        state.Top = Top;
        state.Width = Width;
        state.Height = Height;
        state.Opacity = _windowBehavior.WindowOpacity;
        state.IsMaximized = _isMaximized;
        state.LastUrl = WebView.CoreWebView2?.Source ?? AppConstants.DefaultHomeUrl;
        state.IsMuted = WebView.CoreWebView2?.IsMuted ?? false;
        WindowStateService.Instance.Save(state);
    }

#endregion

#region WebView2 Initialization

    /// <summary>
    /// 获取 WebView2 UserDataFolder 路径
    /// 用于持久化 Cookie 和其他用户数据
    /// </summary>
    private static string GetUserDataFolder()
    {
        return AppPaths.WebView2DataDirectory;
    }

    /// <summary>
    /// 初始化 WebView2 控件
    /// 注意：此方法为 async void，是构造函数中调用异步方法的标准模式
    /// 所有异常都在方法内部处理，不会导致未处理异常
    /// </summary>
    private async void InitializeWebView()
    {
        try
        {
            var userDataFolder = GetUserDataFolder();

            // 确保目录存在
            Directory.CreateDirectory(userDataFolder);

            // 创建 WebView2 环境选项，允许自动播放
            var options = new CoreWebView2EnvironmentOptions {// 允许自动播放媒体（禁用自动播放限制）
                                                              AdditionalBrowserArguments =
                                                                  "--autoplay-policy=no-user-gesture-required"
            };

            // 创建 WebView2 环境，指定 UserDataFolder 以实现 Cookie 持久化
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: options);

            // 初始化 WebView2
            await WebView.EnsureCoreWebView2Async(env);

            // 注入所有脚本（滚动条样式、控制按钮、拖动区域）
            await ScriptInjector.InjectAllAsync(WebView);

            // 监听来自网页的消息
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // 监听导航完成事件
            WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // 监听 URL 变化（包括 SPA 路由变化）
            WebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

            // 拦截新窗口请求，在当前窗口打开而非弹出新窗口
            WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // 附加字幕服务以拦截字幕数据
            SubtitleService.Instance.AttachToWebView(WebView.CoreWebView2);

            // 启动视频时间同步
            StartVideoTimeSync();

            // 从保存的状态加载 URL 和静音设置
            var state = WindowStateService.Instance.Load();

            // 恢复静音状态
            WebView.CoreWebView2.IsMuted = state.IsMuted;

            // 应用透明度
            _windowBehavior.ApplyOpacity();

            // 导航到上次访问的页面（如果有）
            var urlToLoad = !string.IsNullOrWhiteSpace(state.LastUrl) ? state.LastUrl : AppConstants.DefaultHomeUrl;
            WebView.CoreWebView2.Navigate(urlToLoad);
        }
        catch (Exception ex)
        {
            // async void 方法必须在内部处理所有异常
            MessageBox.Show($"WebView2 初始化失败：{ex.Message}\n\n请确保已安装 WebView2 Runtime。", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 处理来自网页的消息
    /// </summary>
    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(message))
            return;

        // 检查是否是 JSON 格式的字幕消息
        if (message.StartsWith("{") && message.Contains("\"type\""))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("type", out var typeEl))
                {
                    var type = typeEl.GetString();
                    if (type == "subtitle_url" || type == "subtitle_data" || type == "subtitle_error" ||
                        type == "subtitle_info")
                    {
                        SubtitleService.Instance.HandleSubtitleMessage(message);
                        return;
                    }
                }
            }
            catch
            {
                // 不是有效的 JSON，继续处理为普通消息
            }
        }

        switch (message)
        {
        case "minimize":
            WindowState = System.Windows.WindowState.Minimized;
            break;

        case "maximize":
            ToggleMaximize();
            break;

        case "close":
            Close();
            break;

        case "drag":
            // 使用 Win32 API 启动拖动（不依赖鼠标状态）
            Dispatcher.BeginInvoke(() =>
                                   { Win32Helper.StartMove(this); });
            break;
        }
    }

    /// <summary>
    /// 导航完成事件处理
    /// </summary>
    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // 触发导航状态变化事件
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);

        // 记录到历史（仅成功的导航）
        if (e.IsSuccess && WebView.CoreWebView2 != null)
        {
            var url = WebView.CoreWebView2.Source;
            var title = WebView.CoreWebView2.DocumentTitle;

            // 过滤掉空白页和内部页面
            if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("about:") && !url.StartsWith("data:"))
            {
                DataService.Instance.AddHistory(url, title);
            }

            // 注意：字幕获取现在由被动拦截处理（SubtitleService.OnWebResourceResponseReceived）
            // 不需要在这里清除字幕或主动请求
        }
    }

    /// <summary>
    /// URL 变化事件处理
    /// </summary>
    private string _lastSubtitleUrl = string.Empty;
    private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        // 触发 URL 变化事件
        var currentUrl = WebView.CoreWebView2?.Source ?? string.Empty;
        UrlChanged?.Invoke(this, currentUrl);

        // 触发导航状态变化事件
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);

        // 广播 urlChanged 事件到插件
        if (!string.IsNullOrEmpty(currentUrl))
        {
            PluginHost.Instance.BroadcastUrlChanged(currentUrl);
        }

        // 注意：字幕获取现在由被动拦截处理（SubtitleService.OnWebResourceResponseReceived）
        // 不需要在这里主动请求字幕
    }

    /// <summary>
    /// 提取 B站视频标识（BV号+分P参数）
    /// </summary>
    private string ExtractBilibiliVideoKey(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath; // e.g., /video/BV1234567890/
            var query = uri.Query;       // e.g., ?p=2

            // 提取 BV 号
            var bvMatch = System.Text.RegularExpressions.Regex.Match(path, @"BV\w+");
            if (bvMatch.Success)
            {
                var bv = bvMatch.Value;
                // 提取分P参数
                var pMatch = System.Text.RegularExpressions.Regex.Match(query, @"[?&]p=(\d+)");
                var p = pMatch.Success ? pMatch.Groups[1].Value : "1";
                return $"{bv}_p{p}";
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    /// <summary>
    /// 拦截新窗口请求，在当前窗口打开
    /// </summary>
    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // 阻止新窗口弹出
        e.Handled = true;

        // 在当前 WebView 中导航到目标 URL
        WebView.CoreWebView2.Navigate(e.Uri);
    }

    /// <summary>
    /// 切换最大化/还原
    /// </summary>
    public void ToggleMaximize()
    {
        if (!_isMaximized)
        {
            // 最大化前临时禁用穿透模式
            _windowBehavior.SuspendClickThroughForMaximize();

            _restoreBounds = new Rect(Left, Top, Width, Height);
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
            _isMaximized = true;
        }
        else
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            _isMaximized = false;

            // 还原后恢复穿透模式
            _windowBehavior.ResumeClickThroughAfterRestore();
        }
    }

#endregion

#region Public Properties

    /// <summary>
    /// 是否处于最大化状态
    /// </summary>
    public bool IsMaximized => _isMaximized;

    /// <summary>
    /// 是否可以后退
    /// </summary>
    public bool CanGoBack => WebView.CoreWebView2?.CanGoBack ?? false;

    /// <summary>
    /// 是否可以前进
    /// </summary>
    public bool CanGoForward => WebView.CoreWebView2?.CanGoForward ?? false;

    /// <summary>
    /// 当前页面标题
    /// </summary>
    public string CurrentTitle => WebView.CoreWebView2?.DocumentTitle ?? string.Empty;

    /// <summary>
    /// 当前页面 URL
    /// </summary>
    public string CurrentUrl => WebView.CoreWebView2?.Source ?? string.Empty;

#endregion

#region Public Methods

    /// <summary>
    /// 导航到指定 URL
    /// </summary>
    public void Navigate(string url)
    {
        if (WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.Navigate(url);
        }
    }

    /// <summary>
    /// 后退
    /// </summary>
    public void GoBack()
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
        {
            WebView.CoreWebView2.GoBack();
        }
    }

    /// <summary>
    /// 前进
    /// </summary>
    public void GoForward()
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
        {
            WebView.CoreWebView2.GoForward();
        }
    }

    /// <summary>
    /// 刷新
    /// </summary>
    public void Refresh()
    {
        WebView.CoreWebView2?.Reload();
    }

    /// <summary>
    /// 切换视频播放/暂停
    /// </summary>
    public async void TogglePlayAsync()
    {
        if (WebView.CoreWebView2 == null)
            return;

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

        try
        {
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // 忽略脚本执行错误
        }
    }

    /// <summary>
    /// 视频快进/倒退
    /// </summary>
    /// <param name="seconds">秒数，正数前进，负数倒退</param>
    public async void SeekAsync(int seconds)
    {
        if (WebView.CoreWebView2 == null)
            return;

        string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        video.currentTime += {seconds};
                        return video.currentTime;
                    }}
                    return -1;
                }})();
            ";

        try
        {
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // 忽略脚本执行错误
        }
    }

    /// <summary>
    /// 降低透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double DecreaseOpacity()
    {
        return _windowBehavior.DecreaseOpacity();
    }

    /// <summary>
    /// 增加透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double IncreaseOpacity()
    {
        return _windowBehavior.IncreaseOpacity();
    }

    /// <summary>
    /// 切换鼠标穿透模式
    /// </summary>
    /// <returns>是否处于穿透模式</returns>
    public bool ToggleClickThrough()
    {
        return _windowBehavior.ToggleClickThrough();
    }

    /// <summary>
    /// 获取当前透明度百分比
    /// 穿透模式下返回保存的透明度设置，非穿透模式下返回当前窗口透明度
    /// </summary>
    public int OpacityPercent => _windowBehavior.OpacityPercent;

    /// <summary>
    /// 是否处于鼠标穿透模式
    /// </summary>
    public bool IsClickThrough => _windowBehavior.IsClickThrough;

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="config">新配置</param>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _windowBehavior.UpdateConfig(config);
    }

#endregion

#region Event Handlers

    /// <summary>
    /// 窗口源初始化完成
    /// </summary>
    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        // 注册窗口消息钩子用于边缘吸附
        var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        hwndSource?.AddHook(WndProc);

        // 设置窗口不激活样式，防止热键操作时抢夺游戏焦点
        Win32Helper.SetNoActivateStyle(this, true);
    }

    /// <summary>
    /// 窗口消息处理钩子 - 用于实现边缘吸附
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
        case Win32Helper.WM_ENTERSIZEMOVE:
            // 开始拖动：委托给 WindowBehaviorHelper
            _windowBehavior.HandleEnterSizeMove(hwnd);
            break;

        case Win32Helper.WM_EXITSIZEMOVE:
            // 结束拖动：委托给 WindowBehaviorHelper
            _windowBehavior.HandleExitSizeMove();
            break;

        case Win32Helper.WM_MOVING:
            // 窗口移动时的边缘吸附：委托给 WindowBehaviorHelper
            _windowBehavior.HandleWindowMoving(hwnd, lParam);
            break;

        case Win32Helper.WM_SIZING:
            // 窗口调整大小时的边缘吸附：委托给 WindowBehaviorHelper
            _windowBehavior.HandleWindowSizing(wParam, lParam);
            break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 鼠标左键按下：边框区域调整大小，其他区域拖动窗口
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var position = e.GetPosition(this);
        var direction = Win32Helper.GetResizeDirection(this, position, AppConstants.ResizeBorderThickness);

        if (direction != Win32Helper.ResizeDirection.None)
        {
            // 在边框区域，开始调整大小
            Win32Helper.StartResize(this, direction);
        }
        else
        {
            // 非边框区域，拖动窗口
            DragMove();
        }
    }

    /// <summary>
    /// 鼠标移动：更新光标样式
    /// </summary>
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this);
        var direction = Win32Helper.GetResizeDirection(this, position, AppConstants.ResizeBorderThickness);

        Cursor = direction switch { Win32Helper.ResizeDirection.Left => Cursors.SizeWE,
                                    Win32Helper.ResizeDirection.Right => Cursors.SizeWE,
                                    Win32Helper.ResizeDirection.Top => Cursors.SizeNS,
                                    Win32Helper.ResizeDirection.Bottom => Cursors.SizeNS,
                                    Win32Helper.ResizeDirection.TopLeft => Cursors.SizeNWSE,
                                    Win32Helper.ResizeDirection.BottomRight => Cursors.SizeNWSE,
                                    Win32Helper.ResizeDirection.TopRight => Cursors.SizeNESW,
                                    Win32Helper.ResizeDirection.BottomLeft => Cursors.SizeNESW,
                                    _ => Cursors.Arrow };
    }

#endregion

#region Video Time Sync

    /// <summary>
    /// 启动视频时间同步
    /// </summary>
    private void StartVideoTimeSync()
    {
        if (_videoTimeSyncTimer != null)
            return;

        _videoTimeSyncTimer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(200) // 每 200ms 同步一次
        };
        _videoTimeSyncTimer.Tick += VideoTimeSyncTimer_Tick;
        _videoTimeSyncTimer.Start();

        LogService.Instance.Debug("PlayerWindow", "视频时间同步已启动");
    }

    /// <summary>
    /// 停止视频时间同步
    /// </summary>
    private void StopVideoTimeSync()
    {
        if (_videoTimeSyncTimer != null)
        {
            _videoTimeSyncTimer.Stop();
            _videoTimeSyncTimer.Tick -= VideoTimeSyncTimer_Tick;
            _videoTimeSyncTimer = null;

            LogService.Instance.Debug("PlayerWindow", "视频时间同步已停止");
        }
    }

    /// <summary>
    /// 视频时间同步定时器回调
    /// </summary>
    private async void VideoTimeSyncTimer_Tick(object? sender, EventArgs e)
    {
        if (WebView.CoreWebView2 == null)
            return;

        try
        {
            // 使用 JavaScript 获取视频当前时间和总时长
            const string script = @"
                    (function() {
                        var video = document.querySelector('video');
                        if (video && !video.paused) {
                            return JSON.stringify({
                                currentTime: video.currentTime,
                                duration: video.duration || 0
                            });
                        }
                        return 'null';
                    })();
                ";

            var result = await WebView.CoreWebView2.ExecuteScriptAsync(script);

            // 解析结果（去除 JSON 字符串的引号）
            if (!string.IsNullOrEmpty(result) && result != "\"null\"" && result != "null")
            {
                // WebView2 返回的 JSON 字符串会被额外包装一层引号
                var jsonStr = result.Trim('"').Replace("\\\"", "\"");
                if (jsonStr.StartsWith("{"))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("currentTime", out var ctEl) &&
                        root.TryGetProperty("duration", out var durEl))
                    {
                        var currentTime = ctEl.GetDouble();
                        var duration = durEl.GetDouble();

                        // 更新字幕服务的当前时间
                        SubtitleService.Instance.UpdateCurrentTime(currentTime);

                        // 广播 timeUpdate 事件到插件
                        PluginHost.Instance.BroadcastTimeUpdate(currentTime, duration);
                    }
                }
            }
        }
        catch
        {
            // 忽略脚本执行错误
        }
    }

#endregion

#region Cursor Detection

    /// <summary>
    /// 初始化鼠标检测
    /// </summary>
    private void InitializeCursorDetection()
    {
        var profile = ProfileManager.Instance.CurrentProfile;
        var config = profile.CursorDetection;

        if (config?.Enabled == true)
        {
            StartCursorDetection(profile);
        }
    }

    /// <summary>
    /// 启动鼠标检测
    /// </summary>
    private void StartCursorDetection(GameProfile profile)
    {
        var config = profile.CursorDetection;
        if (config == null || !config.Enabled)
            return;

        // 保存配置
        _cursorDetectionMinOpacity = config.MinOpacity;

        // 获取目标进程名（从 Activation.Processes 获取第一个）
        string? targetProcess = null;
        if (profile.Activation?.Processes?.Count > 0)
        {
            targetProcess = profile.Activation.Processes[0];
        }

        // 订阅事件
        CursorDetectionService.Instance.CursorShown += OnCursorShown;
        CursorDetectionService.Instance.CursorHidden += OnCursorHidden;

        // 启动检测
        CursorDetectionService.Instance.Start(targetProcess, config.CheckIntervalMs);
    }

    /// <summary>
    /// 停止鼠标检测
    /// </summary>
    private void StopCursorDetection()
    {
        CursorDetectionService.Instance.CursorShown -= OnCursorShown;
        CursorDetectionService.Instance.CursorHidden -= OnCursorHidden;
        CursorDetectionService.Instance.Stop();

        // 如果之前因鼠标检测降低了透明度，恢复
        if (_isOpacityReducedByCursorDetection)
        {
            _windowBehavior.SetInitialOpacity(_opacityBeforeCursorDetection);
            Win32Helper.SetWindowOpacity(this, _opacityBeforeCursorDetection);
            _isOpacityReducedByCursorDetection = false;
        }
    }

    /// <summary>
    /// Profile 切换事件处理
    /// </summary>
    private void OnProfileChanged(object? sender, GameProfile profile)
    {
        // 停止当前鼠标检测
        StopCursorDetection();

        // 如果新 Profile 启用了鼠标检测，启动它
        if (profile.CursorDetection?.Enabled == true)
        {
            StartCursorDetection(profile);
        }
    }

    /// <summary>
    /// 鼠标显示事件处理
    /// </summary>
    private void OnCursorShown(object? sender, EventArgs e)
    {
        // 在 UI 线程执行
        Dispatcher.BeginInvoke(() =>
                               {
                                   // 如果处于穿透模式，不处理
                                   if (_windowBehavior.IsClickThrough)
                                       return;

                                   // 保存当前透明度并降低
                                   if (!_isOpacityReducedByCursorDetection)
                                   {
                                       _opacityBeforeCursorDetection = _windowBehavior.WindowOpacity;
                                       _isOpacityReducedByCursorDetection = true;
                                   }

                                   Win32Helper.SetWindowOpacity(this, _cursorDetectionMinOpacity);
                               });
    }

    /// <summary>
    /// 鼠标隐藏事件处理
    /// </summary>
    private void OnCursorHidden(object? sender, EventArgs e)
    {
        // 在 UI 线程执行
        Dispatcher.BeginInvoke(() =>
                               {
                                   // 如果处于穿透模式，不处理
                                   if (_windowBehavior.IsClickThrough)
                                       return;

                                   // 恢复之前的透明度
                                   if (_isOpacityReducedByCursorDetection)
                                   {
                                       _windowBehavior.SetInitialOpacity(_opacityBeforeCursorDetection);
                                       Win32Helper.SetWindowOpacity(this, _opacityBeforeCursorDetection);
                                       _isOpacityReducedByCursorDetection = false;
                                   }
                               });
    }

#endregion

#region Window Closing

    /// <summary>
    /// 窗口关闭事件处理
    /// </summary>
    private void PlayerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 检查是否需要显示记录提示
        if (_config.PromptRecordOnExit && WebView.CoreWebView2 != null)
        {
            var currentUrl = WebView.CoreWebView2.Source;
            var currentTitle = WebView.CoreWebView2.DocumentTitle;

            // 过滤掉空白页和内部页面
            if (!string.IsNullOrWhiteSpace(currentUrl) && !currentUrl.StartsWith("about:") &&
                !currentUrl.StartsWith("data:"))
            {
                // 检查 URL 是否已记录
                if (ExitRecordPrompt.ShouldShowPrompt(currentUrl))
                {
                    // 显示退出记录提示窗口
                    var exitPrompt = new ExitRecordPrompt(currentUrl, currentTitle);
                    exitPrompt.Owner = this;
                    exitPrompt.ShowDialog();

                    // 根据用户选择执行相应操作
                    switch (exitPrompt.Result)
                    {
                    case ExitRecordPrompt.PromptResult.Cancel:
                        // 取消退出，不做任何操作
                        e.Cancel = true;
                        return;

                    case ExitRecordPrompt.PromptResult.OpenPioneerNotes:
                        // 取消退出，打开开荒笔记窗口
                        e.Cancel = true;
                        var noteWindow = new PioneerNoteWindow();
                        noteWindow.Owner = this;
                        noteWindow.NoteItemSelected += (s, url) => Navigate(url);
                        noteWindow.Show();
                        return;

                    case ExitRecordPrompt.PromptResult.QuickRecord:
                        // 取消退出，打开记录笔记对话框
                        e.Cancel = true;
                        var recordDialog = new RecordNoteDialog(currentUrl, currentTitle);
                        recordDialog.Owner = this;
                        recordDialog.ShowDialog();
                        return;

                    case ExitRecordPrompt.PromptResult.Exit:
                    default:
                        // 继续退出
                        break;
                    }
                }
                // 如果 URL 已记录，直接退出（不显示提示）
            }
        }

        // 保存窗口状态
        SaveWindowState();

        // 停止穿透模式定时器
        _windowBehavior.StopClickThroughTimer();

        // 停止鼠标检测
        StopCursorDetection();

        // 停止视频时间同步
        StopVideoTimeSync();

        // 取消 Profile 事件订阅
        ProfileManager.Instance.ProfileChanged -= OnProfileChanged;

        // 取消事件订阅
        if (WebView.CoreWebView2 != null)
        {
            // 分离字幕服务
            SubtitleService.Instance.DetachFromWebView(WebView.CoreWebView2);

            WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            WebView.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
            WebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
        }
    }

#endregion
}
}
