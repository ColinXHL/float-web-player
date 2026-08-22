using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.ViewModels.Windows;
using Microsoft.Web.WebView2.Core;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// PlayerWindow - 播放器主窗口
///
/// 混合架构：
/// - Code-Behind: WebView2 初始化、窗口行为、UI 逻辑、消息解析
/// - ViewModel: 业务逻辑、状态管理、EventBus 订阅、命令
/// </summary>
public partial class PlayerWindow : Window
{
#region Fields

    // ViewModel
    private readonly PlayerViewModel _viewModel;

// DI注入的服务（保留用于 Code-Behind UI 逻辑）
    private readonly IConfigService _configService;
    private readonly IProfileManager _profileManager;
    private readonly IWindowStateService _windowStateService;
    private readonly ISubtitleService _subtitleService;
    private readonly IDataService _dataService;
    private readonly IPluginHost _pluginHost;
    private readonly ILogService _logService;
    private readonly ICursorDetectionService _cursorDetectionService;
    private readonly IEventBus _eventBus;
    private readonly IDialogFactory _dialogFactory;
    private readonly OsdManager _osdManager;
    private readonly Func<PioneerNoteWindow> _pioneerNoteWindowFactory;
    private readonly ScriptExecutionQueue _scriptQueue;
    private readonly IPioneerNoteService _pioneerNoteService;
    private readonly IMonitorLayoutService _monitorLayoutService;
    private readonly ShutdownCoordinator _shutdownCoordinator;

    /// <summary>
    /// 是否最大化
    /// </summary>
    private bool _isMaximized;

    /// <summary>
    /// 最大化前的窗口边界
    /// </summary>
    private Win32Helper.RECT _restoreBoundsPhysical;

    /// <summary>
    /// 当前窗口所在显示器的设备名称
    /// 用于检测窗口是否移动到了其他显示器
    /// </summary>
    private string? _currentMonitorDeviceName;

    /// <summary>
    /// 是否已安排拖动完成后的显示器更新
    /// </summary>
    private bool _isMonitorUpdateScheduled;

    /// <summary>
    /// 当前配置引用
    /// </summary>
    private AppConfig _config;

    /// <summary>
    /// 窗口行为辅助类（边缘吸附、透明度控制）
    /// </summary>
    private WindowBehaviorHelper _windowBehavior = null!;

    /// <summary>
    /// 视频时间同步定时器
    /// </summary>
    private DispatcherTimer? _videoTimeSyncTimer;
    private bool _isVideoTimeSyncInFlight;
    private int _videoTimeSyncSkippedCount;
    private DateTime _lastSeekBurstUtc = DateTime.MinValue;
    private bool _playbackRestoreHintDeferred;
    private static readonly TimeSpan SeekBurstWindow = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// 当前播放速率
    /// </summary>
    private double _currentPlaybackRate = 1.0;

    /// <summary>
    /// 播放速率最小值
    /// </summary>
    private const double MinPlaybackRate = 0.25;

    /// <summary>
    /// 播放速率最大值
    /// </summary>
    private const double MaxPlaybackRate = 4.0;

    /// <summary>
    /// 播放速率步进值
    /// </summary>
    private const double PlaybackRateStep = 0.25;

    /// <summary>
    /// 窗口是否隐藏
    /// </summary>
    private bool _isHidden;

    /// <summary>
    /// 缩放提示悬停计时器
    /// </summary>
    private readonly DispatcherTimer _resizeHintHoverTimer;

    /// <summary>
    /// 当前是否处于可缩放边缘区域
    /// </summary>
    private bool _isInResizableEdge;

    /// <summary>
    /// 本次进入可缩放边缘后是否已显示提示
    /// </summary>
    private bool _hasShownResizeHintInCurrentHover;
    private CoreWebView2Environment? _webViewEnvironment;
    private Stopwatch? _webViewDisposeStopwatch;
    private int _webViewDisposed;

#endregion

#region Constructor

public PlayerWindow(PlayerViewModel viewModel, IConfigService configService, IProfileManager profileManager,
                        IWindowStateService windowStateService, ISubtitleService subtitleService,
                        IDataService dataService, IPluginHost pluginHost, ILogService logService,
                        ICursorDetectionService cursorDetectionService, IEventBus eventBus,
                        IDialogFactory dialogFactory, OsdManager osdManager,
                        Func<PioneerNoteWindow> pioneerNoteWindowFactory,
                        ScriptExecutionQueue scriptQueue, IPioneerNoteService pioneerNoteService,
                        IMonitorLayoutService monitorLayoutService, ShutdownCoordinator shutdownCoordinator)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
        _subtitleService = subtitleService ?? throw new ArgumentNullException(nameof(subtitleService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _cursorDetectionService =
            cursorDetectionService ?? throw new ArgumentNullException(nameof(cursorDetectionService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        _osdManager = osdManager ?? throw new ArgumentNullException(nameof(osdManager));
        _pioneerNoteWindowFactory =
            pioneerNoteWindowFactory ?? throw new ArgumentNullException(nameof(pioneerNoteWindowFactory));
        _scriptQueue = scriptQueue ?? throw new ArgumentNullException(nameof(scriptQueue));
        _pioneerNoteService = pioneerNoteService ?? throw new ArgumentNullException(nameof(pioneerNoteService));
        _monitorLayoutService = monitorLayoutService ?? throw new ArgumentNullException(nameof(monitorLayoutService));
        _shutdownCoordinator = shutdownCoordinator ?? throw new ArgumentNullException(nameof(shutdownCoordinator));

        _resizeHintHoverTimer = new DispatcherTimer
                                {
                                    Interval = TimeSpan.FromMilliseconds(AppConstants.ResizeHintHoverDelayMs)
                                };
        _resizeHintHoverTimer.Tick += ResizeHintHoverTimer_Tick;

        InitializeComponent();
        _config = _configService.Config;
        InitializeWindowBehavior();
        _shutdownCoordinator.RegisterStage(
            nameof(DisposeWebViewForShutdown), 600, DisposeWebViewForShutdown);
        InitializeWebView();

        // 设置 DataContext（用于数据绑定）
        DataContext = _viewModel;

        // 订阅 ViewModel 的导航请求
        _viewModel.NavigationRequested += OnViewModelNavigationRequested;

        // 窗口关闭时清理
        Closing += PlayerWindow_Closing;

        // 窗口关闭后退出应用
        Closed += (s, e) =>
        { System.Windows.Application.Current.Shutdown(); };

        // 订阅导航控制事件（由 ViewModel 发布，Code-behind 执行 WebView 操作）
        SubscribeToNavigationControlEvents();

        // 订阅透明度相关事件
        SubscribeToOpacityEvents();
        _eventBus.Subscribe<DisplayTopologyChangedEvent>(OnDisplayTopologyChanged);
    }

    /// <summary>
    /// 订阅透明度相关事件
    /// </summary>
    private void SubscribeToOpacityEvents()
    {
        // 订阅透明度查询事件（设置界面查询当前透明度）
        _eventBus.Subscribe<OpacityQueryEvent>(OnOpacityQuery);

        // 订阅透明度变化事件（设置界面修改透明度）
        _eventBus.Subscribe<OpacityChangedEvent>(OnOpacityChangedFromSettings);
    }

    /// <summary>
    /// 处理透明度查询事件
    /// </summary>
    private void OnOpacityQuery(OpacityQueryEvent e)
    {
        e.Callback?.Invoke(_windowBehavior.WindowOpacity);
    }

    /// <summary>
    /// 处理来自设置界面的透明度变化事件
    /// </summary>
    private void OnOpacityChangedFromSettings(OpacityChangedEvent e)
    {
        // 只处理来自设置界面的事件
        if (e.Source != OpacityChangeSource.Settings)
            return;

        // 在 UI 线程执行
        Dispatcher.BeginInvoke(() =>
                               { _windowBehavior.SetOpacity(e.Opacity); });
    }

    /// <summary>
    /// 订阅导航控制事件（后退、前进、刷新）
    /// 这些操作需要直接操作 WebView2，保留在 Code-behind
    /// </summary>
    private void SubscribeToNavigationControlEvents()
    {
        _eventBus.Subscribe<NavigationControlEvent>(OnNavigationControl);
    }

    /// <summary>
    /// 处理导航控制事件
    /// </summary>
    private void OnNavigationControl(NavigationControlEvent e)
    {
        switch (e.Action)
        {
        case NavigationControlAction.Back:
            GoBack();
            break;
        case NavigationControlAction.Forward:
            GoForward();
            break;
        case NavigationControlAction.Refresh:
            Refresh();
            break;
        }
    }

    /// <summary>
    /// 处理 ViewModel 的导航请求
    /// </summary>
    private void OnViewModelNavigationRequested(object? sender, string url)
    {
        Navigate(url);
    }

#endregion

#region Initialization

    /// <summary>
    /// 初始化窗口位置和大小
    /// 从 WindowStateService 加载上次保存的状态，并在可用时恢复到同一显示器
    /// 如果保存的显示器不可用，自动回退到可见区域
    /// </summary>
    private void RestoreWindowPlacement()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitors = Win32Helper.EnumerateMonitors();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (monitors.Count == 0)
        {
            monitors.Add(_monitorLayoutService.GetPrimaryMonitor());
        }

        var state = _windowStateService.Load();
        var placement = PlayerWindowPlacementCalculator.Calculate(
            state,
            monitors,
            AppConstants.MinWindowWidth,
            AppConstants.MinWindowHeight);
        if (!ApplyPhysicalBounds(hwnd, placement.Bounds))
        {
            _logService.Warn(nameof(PlayerWindow), "恢复播放器窗口位置失败");
            return;
        }
        _currentMonitorDeviceName = placement.Monitor.DeviceName;

        if (placement.Recovered ||
            state.PlayerWindowPlacementVersion < AppConstants.PlayerWindowPlacementVersion)
        {
            UpdatePlacementState(state, placement.Bounds, placement.Monitor);
            _windowStateService.Save(state);
        }
    }

    private void EnsureWindowVisibleAfterTopologyChange()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitors = Win32Helper.EnumerateMonitors();
        if (hwnd == IntPtr.Zero || monitors.Count == 0 ||
            !Win32Helper.GetWindowRectangle(hwnd, out var bounds))
        {
            return;
        }

        var target = _monitorLayoutService.GetMonitorFromWindowOrDefault(hwnd);
        var visibleBounds = _isMaximized
            ? target.WorkAreaRect
            : PlayerWindowPlacementCalculator.EnsureVisible(
                bounds,
                target,
                monitors,
                AppConstants.MinimumVisibleWindowDip);
        if (!ApplyPhysicalBounds(hwnd, visibleBounds))
        {
            _logService.Warn(nameof(PlayerWindow), "修正播放器窗口可见位置失败");
            return;
        }
        UpdateCurrentMonitor();
    }

    private static bool ApplyPhysicalBounds(IntPtr hwnd, Win32Helper.RECT bounds)
    {
        return Win32Helper.SetWindowRectangle(
            hwnd,
            bounds.Left,
            bounds.Top,
            Math.Max(1, bounds.Right - bounds.Left),
            Math.Max(1, bounds.Bottom - bounds.Top));
    }

    private static void UpdatePlacementState(
        AkashaNavigator.Models.Config.WindowState state,
        Win32Helper.RECT bounds,
        MonitorInfo monitor)
    {
        var dpiScale = double.IsFinite(monitor.DpiScale) && monitor.DpiScale > 0
            ? monitor.DpiScale
            : 1.0;
        var anchors = PlayerWindowPlacementCalculator.CalculateAnchorRatios(bounds, monitor);
        state.Left = bounds.Left / dpiScale;
        state.Top = bounds.Top / dpiScale;
        state.Width = (bounds.Right - bounds.Left) / dpiScale;
        state.Height = (bounds.Bottom - bounds.Top) / dpiScale;
        state.MonitorDeviceName = monitor.DeviceName;
        state.PlayerWindowPlacementVersion = AppConstants.PlayerWindowPlacementVersion;
        state.PlayerWindowHorizontalAnchorRatio = anchors.Horizontal;
        state.PlayerWindowVerticalAnchorRatio = anchors.Vertical;
    }

    /// <summary>
    /// 初始化窗口行为辅助类
    /// </summary>
    private void InitializeWindowBehavior()
    {
        var state = _windowStateService.Load();
        _windowBehavior = new WindowBehaviorHelper(this, _config, state.Opacity, _monitorLayoutService);
    }

/// <summary>
    /// 保存窗口状态
    /// </summary>
    private void SaveWindowState()
    {
        var state = _windowStateService.Load();
        var hwnd = new WindowInteropHelper(this).Handle;
        var bounds = _isMaximized && IsValidPhysicalBounds(_restoreBoundsPhysical)
            ? _restoreBoundsPhysical
            : default;
        if (!IsValidPhysicalBounds(bounds) && hwnd != IntPtr.Zero)
        {
            Win32Helper.GetWindowRectangle(hwnd, out bounds);
        }

        if (IsValidPhysicalBounds(bounds))
        {
            var monitor = hwnd != IntPtr.Zero
                ? _monitorLayoutService.GetMonitorFromWindow(hwnd)
                : null;
            monitor ??= !string.IsNullOrEmpty(_currentMonitorDeviceName)
                ? _monitorLayoutService.FindMonitorByDeviceName(_currentMonitorDeviceName)
                : null;
            monitor ??= _monitorLayoutService.GetPrimaryMonitor();
            UpdatePlacementState(state, bounds, monitor);
        }

        state.Opacity = _windowBehavior.WindowOpacity;
        state.IsMaximized = false;
        state.LastUrl = WebView.CoreWebView2?.Source ?? AppConstants.DefaultHomeUrl;
        state.IsMuted = WebView.CoreWebView2?.IsMuted ?? false;
        _windowStateService.Save(state);
    }

    private static bool IsValidPhysicalBounds(Win32Helper.RECT bounds) =>
        bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;

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
            _webViewEnvironment = env;
            env.BrowserProcessExited += CoreWebView2Environment_BrowserProcessExited;

            if (Volatile.Read(ref _webViewDisposed) != 0)
            {
                return;
            }

            // 初始化 WebView2
            await WebView.EnsureCoreWebView2Async(env);

            if (Volatile.Read(ref _webViewDisposed) != 0)
            {
                WebView.Dispose();
                return;
            }

            // 注入所有脚本（滚动条样式、控制按钮、拖动区域）
            await ScriptInjector.InjectAllAsync(WebView);

            // 监听来自网页的消息
            WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // 监听导航完成事件
            WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // 监听 URL 变化（包括 SPA 路由变化）
            WebView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

            // 监听页面标题变化
            WebView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;

            // 拦截新窗口请求，在当前窗口打开而非弹出新窗口
            WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // 附加字幕服务以拦截字幕数据
            _subtitleService.AttachToWebView(WebView.CoreWebView2);

            // 启动视频时间同步
            StartVideoTimeSync();

            // 从保存的状态加载 URL 和静音设置
            var state = _windowStateService.Load();

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
            if (Volatile.Read(ref _webViewDisposed) != 0)
            {
                return;
            }

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
                        _subtitleService.HandleSubtitleMessage(message);
                        return;
                    }

                    if (type == "playback_state_debug")
                    {
                        var debugMessage = doc.RootElement.TryGetProperty("message", out var msgEl)
                            ? msgEl.GetString() ?? string.Empty
                            : string.Empty;

                        var debugUrl = doc.RootElement.TryGetProperty("url", out var urlEl)
                            ? urlEl.GetString() ?? string.Empty
                            : string.Empty;

                        var debugData = doc.RootElement.TryGetProperty("data", out var dataEl)
                            ? dataEl.GetRawText()
                            : "null";

                        _logService.Debug(nameof(PlayerWindow),
                                          "PlaybackStateDebug: {DebugMessage}, Url={Url}, Data={Data}",
                                          debugMessage, debugUrl, debugData);
                        return;
                    }

                    if (type == "playback_rate_sync")
                    {
                        var syncSource = doc.RootElement.TryGetProperty("source", out var syncSourceEl)
                            ? syncSourceEl.GetString() ?? string.Empty
                            : string.Empty;

                        if (doc.RootElement.TryGetProperty("rate", out var rateEl) &&
                            rateEl.TryGetDouble(out var rate) &&
                            double.IsFinite(rate) &&
                            rate is >= MinPlaybackRate and <= MaxPlaybackRate &&
                            string.Equals(syncSource, "bilibili-rate-menu", StringComparison.Ordinal) &&
                            IsBilibiliVideoUrl(e.Source))
                        {
                            _currentPlaybackRate = rate;
                            _logService.Debug(nameof(PlayerWindow), "Playback rate synced from web page: {Rate}", rate);
                        }

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

    private void CoreWebView2Environment_BrowserProcessExited(
        object? sender,
        CoreWebView2BrowserProcessExitedEventArgs e)
    {
        if (sender is CoreWebView2Environment environment)
        {
            environment.BrowserProcessExited -= CoreWebView2Environment_BrowserProcessExited;
        }

        _logService.Info(
            nameof(PlayerWindow),
            "WebView2 浏览器进程组已退出: ProcessId={ProcessId}, ExitKind={ExitKind}, Dispose后耗时={ElapsedMilliseconds}ms",
            e.BrowserProcessId,
            e.BrowserProcessExitKind,
            _webViewDisposeStopwatch?.ElapsedMilliseconds ?? 0);
    }

    private static bool IsBilibiliVideoUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return (string.Equals(uri.Host, "bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase)) &&
               uri.AbsolutePath.StartsWith("/video/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 导航完成事件处理
    /// </summary>
    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // 通知 ViewModel 导航完成
        _viewModel.OnNavigationCompleted(e.IsSuccess);

        _ = TriggerPlaybackStateRestoreHintAsync();

        // 发布导航状态变化事件
        _eventBus.Publish(new NavigationStateChangedEvent { CanGoBack = CanGoBack, CanGoForward = CanGoForward });

        // 记录到历史（仅成功的导航）
        if (e.IsSuccess && WebView.CoreWebView2 != null)
        {
            var url = WebView.CoreWebView2.Source;
            var title = WebView.CoreWebView2.DocumentTitle;

            // 过滤掉空白页和内部页面
            if (!string.IsNullOrWhiteSpace(url) && !url.StartsWith("about:") && !url.StartsWith("data:"))
            {
                _dataService.AddHistory(url, title);
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
        var currentUrl = WebView.CoreWebView2?.Source ?? string.Empty;

        // 通知 ViewModel URL 变化
        _viewModel.UpdateCurrentUrl(currentUrl);

        // 发布 URL 变化事件
        _eventBus.Publish(new UrlChangedEvent { Url = currentUrl });

        // 发布导航状态变化事件
        _eventBus.Publish(new NavigationStateChangedEvent { CanGoBack = CanGoBack, CanGoForward = CanGoForward });

        // 广播 urlChanged 事件到插件
        if (!string.IsNullOrEmpty(currentUrl))
        {
            _pluginHost.BroadcastUrlChanged(currentUrl);
        }

        // 注意：字幕获取现在由被动拦截处理（SubtitleService.OnWebResourceResponseReceived）
        // 不需要在这里主动请求字幕
    }

    /// <summary>
    /// 页面标题变化事件处理
    /// </summary>
    private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
    {
        var currentTitle = WebView.CoreWebView2?.DocumentTitle ?? string.Empty;

        // 发布标题变化事件
        _eventBus.Publish(new TitleChangedEvent { Title = currentTitle });
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
    /// 使用当前显示器的工作区域，确保最大化到窗口所在显示器而非主显示器
    /// </summary>
    public void ToggleMaximize()
    {
        if (!_isMaximized)
        {
            // 最大化前强制结束窥视
            ForceEndPeek();

            _windowBehavior.SuspendClickThroughForMaximize();

            // 暂停鼠标检测（全屏时不需要降低透明度）
            _cursorDetectionService.Suspend();

            // 保存当前窗口物理像素边界用于跨 DPI 精确还原
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Helper.GetWindowRectangle(hwnd, out _restoreBoundsPhysical);

            // 使用当前显示器的工作区域进行最大化
            var monitor = _monitorLayoutService.GetMonitorFromWindowOrDefault(hwnd);
            ApplyPhysicalBounds(hwnd, monitor.WorkAreaRect);
            _isMaximized = true;

            // 最大化时穿透已暂停，通知控制栏恢复自动显示
            // 注意：SuspendClickThroughForMaximize 保留了 _isClickThrough 标志但实际穿透已禁用，
            // 因此需要发送 IsEffectiveClickThrough = false 表示穿透实际未生效
            _eventBus.Publish(new Core.Events.Events.ClickThroughChangedEvent
            {
                IsEffectiveClickThrough = false,
                Source = "maximize_suspend"
            });
        }
        else
        {
            _isMaximized = false;
            if (IsValidPhysicalBounds(_restoreBoundsPhysical))
            {
                ApplyPhysicalBounds(new WindowInteropHelper(this).Handle, _restoreBoundsPhysical);
                EnsureWindowVisibleAfterTopologyChange();
            }

            // 还原后恢复穿透模式
            _windowBehavior.ResumeClickThroughAfterRestore();

            // 恢复鼠标检测（会立即检测当前状态）
            _cursorDetectionService.Resume();

            // 还原后通知控制栏当前的穿透状态
            // 如果穿透模式恢复，控制栏应被抑制；否则恢复正常显示
            _eventBus.Publish(new Core.Events.Events.ClickThroughChangedEvent
            {
                IsEffectiveClickThrough = IsEffectiveClickThrough,
                Source = "maximize_resume"
            });
        }

        // 更新当前显示器信息并发布事件
        UpdateCurrentMonitor();
    }

    /// <summary>
    /// 更新当前显示器信息，如果显示器发生变化则发布事件
    /// </summary>
    private void UpdateCurrentMonitor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var currentMonitor = _monitorLayoutService.GetMonitorFromWindow(hwnd);
        var newDeviceName = currentMonitor?.DeviceName;

        if (newDeviceName != _currentMonitorDeviceName)
        {
            _currentMonitorDeviceName = newDeviceName;
            _eventBus.Publish(new PlayerMonitorChangedEvent { MonitorInfo = currentMonitor });
        }
    }

    /// <summary>
    /// 在拖动/缩放完全结束并且 UI 稳定后，再更新当前显示器
    /// </summary>
    private void ScheduleUpdateCurrentMonitorAfterMoveComplete()
    {
        if (_isMonitorUpdateScheduled)
            return;

        _isMonitorUpdateScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            _isMonitorUpdateScheduled = false;
            UpdateCurrentMonitor();
        }));
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

    /// <summary>
    /// 是否处于自动点击穿透模式（插件控制）
    /// </summary>
    public bool IsAutoClickThrough => _windowBehavior.IsAutoClickThrough;

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
    public async Task TogglePlayAsync()
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
            await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, "TogglePlay", 1200,
                                            priority: ScriptExecutionQueue.ScriptExecutionPriority.High);
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PlayerWindow), ex, "TogglePlayAsync failed");
        }
    }

    private async Task TriggerPlaybackStateRestoreHintAsync()
    {
        if (IsSeekBurstActive())
        {
            _playbackRestoreHintDeferred = true;
            return;
        }

        await TriggerPlaybackStateRestoreHintCoreAsync();
    }

    private async Task TriggerPlaybackStateRestoreHintCoreAsync()
    {
        if (WebView.CoreWebView2 == null)
            return;

        const string script = @"
            (function() {
                try {
                    window.dispatchEvent(new CustomEvent('akasha:navigation-completed'));
                } catch (_) {
                }
            })();
        ";

        try
        {
            await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, "PlaybackStateRestoreHint", 1200,
                                            priority: ScriptExecutionQueue.ScriptExecutionPriority.Normal,
                                            coalesceKey: "playback-restore-hint");
        }
        catch (Exception ex)
        {
            _logService.Debug(nameof(PlayerWindow), "TriggerPlaybackStateRestoreHintAsync failed: {ErrorMessage}",
                              ex.Message);
        }
    }

    /// <summary>
    /// 视频快进/倒退
    /// </summary>
    /// <param name="seconds">秒数，正数前进，负数倒退</param>
    public async Task SeekAsync(int seconds)
    {
        if (WebView.CoreWebView2 == null)
            return;

        MarkSeekBurst();

        string script = $@"
                (function() {{
                    var video = document.querySelector('video');
                    if (video) {{
                        var wasPaused = !!video.paused;
                        video.currentTime += {seconds};

                        if (wasPaused) {{
                            try {{
                                var playPromise = video.play();
                                if (playPromise && typeof playPromise.catch === 'function') {{
                                    playPromise.catch(function() {{ }});
                                }}
                            }} catch (_) {{
                            }}
                        }}

                        return video.currentTime;
                    }}
                    return -1;
                }})();
            ";

        try
        {
            await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, $"Seek({seconds}s)", 1200,
                                            priority: ScriptExecutionQueue.ScriptExecutionPriority.High,
                                            coalesceKey: "seek");
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PlayerWindow), ex, "SeekAsync failed (seconds={Seconds})", seconds);
        }
    }

    /// <summary>
    /// 降低透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double DecreaseOpacity()
    {
        var opacity = _windowBehavior.DecreaseOpacity();
        // 发布透明度变化事件
        _eventBus.Publish(new OpacityChangedEvent { Opacity = opacity, Source = OpacityChangeSource.Hotkey });
        return opacity;
    }

    /// <summary>
    /// 增加透明度
    /// </summary>
    /// <returns>当前透明度</returns>
    public double IncreaseOpacity()
    {
        var opacity = _windowBehavior.IncreaseOpacity();
        // 发布透明度变化事件
        _eventBus.Publish(new OpacityChangedEvent { Opacity = opacity, Source = OpacityChangeSource.Hotkey });
        return opacity;
    }

    /// <summary>
    /// 切换鼠标穿透模式
    /// </summary>
    /// <returns>是否处于穿透模式</returns>
    public bool ToggleClickThrough()
    {
        var result = _windowBehavior.ToggleClickThrough();

        // 注意：不再隐藏 WebView2，因为这会导致窗口句柄问题
        // WebView2 的穿透需要通过其他方式处理
        // WebView.Visibility = result ? Visibility.Hidden : Visibility.Visible;

        BroadcastClickThroughChanged("manual");

        return result;
    }

    /// <summary>
    /// 设置自动点击穿透状态（由插件控制）
    /// </summary>
    /// <param name="enabled">是否启用自动穿透</param>
    public void SetAutoClickThrough(bool enabled)
    {
        _windowBehavior.SetAutoClickThrough(enabled);
        BroadcastClickThroughChanged("auto");
    }

    /// <summary>
    /// 重置自动点击穿透状态（插件卸载或禁用时调用）
    /// </summary>
    public void ResetAutoClickThrough()
    {
        _windowBehavior.ResetAutoClickThrough();
        BroadcastClickThroughChanged("reset");
    }

    /// <summary>
    /// 获取当前透明度百分比
    /// 穿透模式下返回保存的透明度设置，非穿透模式下返回当前窗口透明度
    /// </summary>
    public int OpacityPercent => _windowBehavior.OpacityPercent;

    /// <summary>
    /// 获取当前实际窗口透明度（通过 Win32 API 设置的值，非 WPF Window.Opacity）
    /// </summary>
    public double ActualOpacity => _windowBehavior.WindowOpacity;

    /// <summary>
    /// 是否处于鼠标穿透模式
    /// </summary>
    public bool IsClickThrough => _windowBehavior.IsClickThrough;

    /// <summary>
    /// 是否处于有效鼠标穿透模式（手动或自动）
    /// </summary>
    public bool IsEffectiveClickThrough => _windowBehavior.IsEffectiveClickThrough;

private void BroadcastClickThroughChanged(string source)
    {
        var payload = new {
            enabled = IsEffectiveClickThrough,
            manualEnabled = IsClickThrough,
            autoEnabled = IsAutoClickThrough,
            source
        };

        _pluginHost.BroadcastEvent(AkashaNavigator.Plugins.Utils.EventManager.ClickThroughChanged, payload);
        _pluginHost.BroadcastEvent($"window.{AkashaNavigator.Plugins.Utils.EventManager.ClickThroughChanged}", payload);

        // 通知 ControlBarWindow 穿透状态变化（用于抑制控制栏自动显示）
        _eventBus.Publish(new Core.Events.Events.ClickThroughChangedEvent
        {
            IsEffectiveClickThrough = IsEffectiveClickThrough,
            Source = source
        });
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="config">新配置</param>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _windowBehavior.UpdateConfig(config);
        _windowBehavior.SetPeekConfig(config.EnableHoldToPeek, config.PeekOpacity);
    }

#region Peek

    /// <summary>
    /// 设置窥视按键 held 状态
    /// </summary>
    /// <param name="held">是否按住窥视按键</param>
    public void SetPeekHeld(bool held)
    {
        _windowBehavior.SetPeekHeld(held);
    }

    /// <summary>
    /// 强制结束窥视（最大化、隐藏、关闭时调用）
    /// </summary>
    public void ForceEndPeek()
    {
        _windowBehavior.ForceEndPeek();
    }

#endregion

    /// <summary>
    /// 重置透明度到 100%
    /// </summary>
    public void ResetOpacity()
    {
        _windowBehavior.SetOpacity(1.0);
        // 发布透明度变化事件
        _eventBus.Publish(new OpacityChangedEvent { Opacity = 1.0, Source = OpacityChangeSource.Hotkey });
    }

    /// <summary>
    /// 获取当前播放速率
    /// </summary>
    public double CurrentPlaybackRate => _currentPlaybackRate;

    /// <summary>
    /// 设置视频播放速率
    /// </summary>
    /// <param name="rate">播放速率 (0.25-4.0)</param>
    public async Task SetPlaybackRateAsync(double rate)
    {
        if (WebView.CoreWebView2 == null)
            return;

        // 限制在有效范围内
        rate = Math.Clamp(rate, MinPlaybackRate, MaxPlaybackRate);
        _currentPlaybackRate = rate;

        string script = $@"
            (function() {{
                window.dispatchEvent(new CustomEvent('akasha:set-playback-rate', {{
                    detail: {{ rate: {rate.ToString(System.Globalization.CultureInfo.InvariantCulture)} }}
                }}));
                var video = document.querySelector('video');
                if (video) {{
                    video.playbackRate = {rate.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                    return video.playbackRate;
                }}
                return -1;
            }})();
        ";

        try
        {
            await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, $"SetPlaybackRate({rate})", 1200,
                                            priority: ScriptExecutionQueue.ScriptExecutionPriority.High,
                                            coalesceKey: "playback-rate");
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PlayerWindow), ex, "SetPlaybackRateAsync failed (rate={Rate})", rate);
        }
    }

    private async Task<double?> TryGetLivePlaybackRateAsync()
    {
        if (WebView.CoreWebView2 == null)
        {
            return null;
        }

        const string script = @"
            (function() {
                var video = document.querySelector('video');
                return video ? video.playbackRate : null;
            })();
        ";

        try
        {
            var raw = await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, "GetPlaybackRate", 1200,
                                                      priority: ScriptExecutionQueue.ScriptExecutionPriority.High,
                                                      coalesceKey: "playback-rate-read");

            if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
            {
                _logService.Debug(nameof(PlayerWindow), "Live playback rate unavailable; falling back to cached rate");
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(raw);
                if (document.RootElement.ValueKind != JsonValueKind.Number || !document.RootElement.TryGetDouble(out var rate))
                {
                    _logService.Debug(nameof(PlayerWindow), "Live playback rate payload was not a number; falling back to cached rate");
                    return null;
                }

                if (!double.IsFinite(rate) || rate < MinPlaybackRate || rate > MaxPlaybackRate)
                {
                    _logService.Debug(nameof(PlayerWindow),
                                      "Live playback rate was out of range; falling back to cached rate (Rate={Rate})",
                                      rate);
                    return null;
                }

                return rate;
            }
            catch (JsonException)
            {
                _logService.Debug(nameof(PlayerWindow), "Live playback rate payload could not be parsed; falling back to cached rate");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PlayerWindow), ex, "TryGetLivePlaybackRateAsync failed");
            return null;
        }
    }

    /// <summary>
    /// 增加播放速率 (+0.25)
    /// </summary>
    public async Task IncreasePlaybackRateAsync()
    {
        var currentRate = await TryGetLivePlaybackRateAsync() ?? _currentPlaybackRate;
        await SetPlaybackRateAsync(currentRate + PlaybackRateStep);
    }

    /// <summary>
    /// 减少播放速率 (-0.25)
    /// </summary>
    public async Task DecreasePlaybackRateAsync()
    {
        var currentRate = await TryGetLivePlaybackRateAsync() ?? _currentPlaybackRate;
        await SetPlaybackRateAsync(currentRate - PlaybackRateStep);
    }

    /// <summary>
    /// 重置播放速率到 1.0x
    /// </summary>
    public async Task ResetPlaybackRateAsync()
    {
        await SetPlaybackRateAsync(1.0);
    }

    /// <summary>
    /// 切换窗口可见性
    /// </summary>
    public void ToggleVisibility()
    {
        if (_isHidden)
        {
            Show();
            _isHidden = false;
        }
        else
        {
            // 隐藏前强制结束窥视
            ForceEndPeek();
            Hide();
            _isHidden = true;
        }
    }

    /// <summary>
    /// 窗口是否隐藏
    /// </summary>
    public bool IsHidden => _isHidden;

#endregion

#region Event Handlers

    /// <summary>
    /// 窗口源初始化完成
    /// </summary>
    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        RestoreWindowPlacement();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(EnsureWindowVisibleAfterTopologyChange));

        // 注册窗口消息钩子用于边缘吸附
        var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        hwndSource?.AddHook(WndProc);

        // 设置窗口不激活样式，防止热键操作时抢夺游戏焦点
        Win32Helper.SetNoActivateStyle(this, true);
    }

    private void OnDisplayTopologyChanged(DisplayTopologyChangedEvent e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnDisplayTopologyChanged(e));
            return;
        }

        EnsureWindowVisibleAfterTopologyChange();
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
            // 拖动完全结束后再检测窗口是否移动到了其他显示器，通知控制栏跟随
            ScheduleUpdateCurrentMonitorAfterMoveComplete();
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

        if (_isMaximized)
        {
            StopResizeHintHover();
            _isInResizableEdge = false;
            _hasShownResizeHintInCurrentHover = false;
            Cursor = Cursors.Arrow;
            return;
        }

        var position = e.GetPosition(this);
        var direction = Win32Helper.GetResizeDirection(this, position, AppConstants.ResizeBorderThickness);

        bool isInResizableEdge = direction != Win32Helper.ResizeDirection.None;

        if (isInResizableEdge && !_isInResizableEdge)
        {
            _isInResizableEdge = true;
            _hasShownResizeHintInCurrentHover = false;
            StartResizeHintHover();
        }
        else if (!isInResizableEdge && _isInResizableEdge)
        {
            _isInResizableEdge = false;
            _hasShownResizeHintInCurrentHover = false;
            StopResizeHintHover();
        }

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

    /// <summary>
    /// 鼠标离开窗口时重置缩放提示状态
    /// </summary>
    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isInResizableEdge = false;
        _hasShownResizeHintInCurrentHover = false;
        StopResizeHintHover();
    }

    /// <summary>
    /// 启动缩放提示悬停计时器
    /// </summary>
    private void StartResizeHintHover()
    {
        if (_resizeHintHoverTimer.IsEnabled)
            return;

        _resizeHintHoverTimer.Start();
    }

    /// <summary>
    /// 停止缩放提示悬停计时器
    /// </summary>
    private void StopResizeHintHover()
    {
        if (_resizeHintHoverTimer.IsEnabled)
        {
            _resizeHintHoverTimer.Stop();
        }
    }

    /// <summary>
    /// 缩放提示悬停计时器回调
    /// </summary>
    private void ResizeHintHoverTimer_Tick(object? sender, EventArgs e)
    {
        StopResizeHintHover();

        if (_isMaximized || !_isInResizableEdge || _hasShownResizeHintInCurrentHover)
            return;

if (_config.EnableOsd)
        {
            _osdManager.ShowMessage(AppConstants.ResizeHintMessage, AppConstants.ResizeHintIcon);
        }
        _hasShownResizeHintInCurrentHover = true;
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

        _videoTimeSyncTimer =
            new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AppConstants.VideoTimeSyncIntervalMs) };
        _videoTimeSyncTimer.Tick += VideoTimeSyncTimer_Tick;
        _videoTimeSyncTimer.Start();

        _logService.Debug(nameof(PlayerWindow), "视频时间同步已启动");
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

            _logService.Debug(nameof(PlayerWindow), "视频时间同步已停止");
        }
    }

    /// <summary>
    /// 视频时间同步定时器回调
    /// </summary>
    private async void VideoTimeSyncTimer_Tick(object? sender, EventArgs e)
    {
        if (WebView.CoreWebView2 == null)
            return;

        if (IsSeekBurstActive())
        {
            _videoTimeSyncSkippedCount++;
            return;
        }

        if (_playbackRestoreHintDeferred)
        {
            _playbackRestoreHintDeferred = false;
            await TriggerPlaybackStateRestoreHintCoreAsync();
        }

        // 避免重入：上一次同步未完成时直接跳过
        if (_isVideoTimeSyncInFlight)
        {
            _videoTimeSyncSkippedCount++;
            if (_videoTimeSyncSkippedCount % 50 == 0)
            {
                _logService.Warn(nameof(PlayerWindow),
                                 "VideoTimeSync skipped due to in-flight execution (SkippedCount={SkippedCount})",
                                 _videoTimeSyncSkippedCount);
            }
            return;
        }

        // 队列拥塞时主动退避，优先保障用户交互类脚本
        if (_scriptQueue.IsBacklogged(AppConstants.VideoTimeSyncQueueBackpressureThreshold))
        {
            _videoTimeSyncSkippedCount++;
            if (_videoTimeSyncSkippedCount % 50 == 0)
            {
                _logService.Warn(nameof(PlayerWindow),
                                 "VideoTimeSync skipped due to script queue pressure (SkippedCount={SkippedCount}, Queue={Queued})",
                                 _videoTimeSyncSkippedCount, _scriptQueue.QueuedCount);
            }
            if (_videoTimeSyncTimer != null &&
                _videoTimeSyncTimer.Interval.TotalMilliseconds < AppConstants.VideoTimeSyncBackoffIntervalMs)
            {
                _videoTimeSyncTimer.Interval = TimeSpan.FromMilliseconds(AppConstants.VideoTimeSyncBackoffIntervalMs);
            }
            return;
        }

        _isVideoTimeSyncInFlight = true;

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

            var result = await _scriptQueue.ExecuteAsync(WebView.CoreWebView2, script, "VideoTimeSync", 2000,
                                                         priority: ScriptExecutionQueue.ScriptExecutionPriority.Low,
                                                         coalesceKey: "video-time-sync");

            // 同步恢复后回到常规频率
            if (_videoTimeSyncTimer != null &&
                _videoTimeSyncTimer.Interval.TotalMilliseconds != AppConstants.VideoTimeSyncIntervalMs)
            {
                _videoTimeSyncTimer.Interval = TimeSpan.FromMilliseconds(AppConstants.VideoTimeSyncIntervalMs);
            }

            if (_videoTimeSyncSkippedCount > 0)
            {
                _videoTimeSyncSkippedCount = 0;
            }

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
                        _subtitleService.UpdateCurrentTime(currentTime);

                        // 广播 timeUpdate 事件到插件
                        _pluginHost.BroadcastTimeUpdate(currentTime, duration);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PlayerWindow), ex, "VideoTimeSyncTimer_Tick failed");
        }
        finally
        {
            _isVideoTimeSyncInFlight = false;
        }
    }

    private void MarkSeekBurst()
    {
        _lastSeekBurstUtc = DateTime.UtcNow;
    }

    private bool IsSeekBurstActive()
    {
        return DateTime.UtcNow - _lastSeekBurstUtc < SeekBurstWindow;
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
                if (ExitRecordPrompt.ShouldShowPrompt(currentUrl, _pioneerNoteService))
                {
                    // 显示退出记录提示窗口
                    var exitPrompt = _dialogFactory.CreateExitRecordPrompt(currentUrl, currentTitle);
                    exitPrompt.Owner = this;
                    exitPrompt.ShowDialog();

                    // 根据用户选择执行相应操作
                    switch (exitPrompt.Result)
                    {
                    case PromptResult.Cancel:
                        // 取消退出，不做任何操作
                        e.Cancel = true;
                        return;

                    case PromptResult.OpenPioneerNotes:
                        // 取消退出，打开开荒笔记窗口
                        e.Cancel = true;
                        var pioneerNoteWindow = _pioneerNoteWindowFactory();
                        pioneerNoteWindow.Owner = this;
                        pioneerNoteWindow.NoteItemSelected += (s, url) => Navigate(url);
                        pioneerNoteWindow.Show();
                        return;

                    case PromptResult.QuickRecord:
                        // 打开记录笔记对话框
                        var recordDialog = _dialogFactory.CreateRecordNoteDialog(currentUrl, currentTitle);
                        recordDialog.Owner = this;
                        recordDialog.ShowDialog();

                        // 如果用户成功保存了笔记，继续退出；否则取消退出
                        if (recordDialog.Result && recordDialog.CreatedNote != null)
                        {
                            // 保存成功，继续退出
                            break;
                        }
                        else
                        {
                            // 取消或保存失败，取消退出
                            e.Cancel = true;
                            return;
                        }

                    case PromptResult.Exit:
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

        _shutdownCoordinator.Shutdown();
    }

    private void DisposeWebViewForShutdown()
    {
        if (Interlocked.Exchange(ref _webViewDisposed, 1) != 0)
        {
            return;
        }

        _webViewDisposeStopwatch = Stopwatch.StartNew();

        StopResizeHintHover();
        _resizeHintHoverTimer.Tick -= ResizeHintHoverTimer_Tick;
        _windowBehavior.StopClickThroughTimer();
        StopVideoTimeSync();

        _viewModel.NavigationRequested -= OnViewModelNavigationRequested;
        _eventBus.Unsubscribe<NavigationControlEvent>(OnNavigationControl);
        _eventBus.Unsubscribe<OpacityQueryEvent>(OnOpacityQuery);
        _eventBus.Unsubscribe<OpacityChangedEvent>(OnOpacityChangedFromSettings);
        _eventBus.Unsubscribe<DisplayTopologyChangedEvent>(OnDisplayTopologyChanged);

        var coreWebView2 = WebView.CoreWebView2;
        if (coreWebView2 != null)
        {
            _subtitleService.DetachFromWebView(coreWebView2);
            coreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            coreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
            coreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
            coreWebView2.DocumentTitleChanged -= CoreWebView2_DocumentTitleChanged;
            coreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
        }

        WebView.Dispose();
        _webViewDisposeStopwatch.Stop();
        _logService.Info(
            nameof(PlayerWindow), "WebView2 已显式释放，耗时 {ElapsedMilliseconds}ms",
            _webViewDisposeStopwatch.ElapsedMilliseconds);
    }

#endregion
}
}
