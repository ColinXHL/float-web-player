using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;
using Microsoft.Web.WebView2.Core;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;

namespace FloatWebPlayer.Views
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
        /// 是否处于鼠标穿透模式
        /// </summary>
        private bool _isClickThrough;

        /// <summary>
        /// 穿透模式前保存的透明度
        /// </summary>
        private double _opacityBeforeClickThrough = 1.0;

        /// <summary>
        /// 当前窗口透明度（使用 Win32 API 控制）
        /// </summary>
        private double _windowOpacity = 1.0;

        /// <summary>
        /// 穿透模式下的鼠标位置检测定时器
        /// </summary>
        private DispatcherTimer? _clickThroughTimer;

        /// <summary>
        /// 记录穿透模式下鼠标是否在窗口内
        /// </summary>
        private bool _isCursorInWindowWhileClickThrough;

        /// <summary>
        /// 当前配置引用
        /// </summary>
        private AppConfig _config;

        /// <summary>
        /// 拖动开始时鼠标相对窗口左上角的偏移（物理像素）
        /// </summary>
        private Win32Helper.POINT _dragOffset;

        /// <summary>
        /// 是否正在拖动窗口
        /// </summary>
        private bool _isDragging;

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

        #endregion

        #region Constructor

        public PlayerWindow()
        {
            InitializeComponent();
            _config = ConfigService.Instance.Config;
            InitializeWindowPosition();
            InitializeWebView();
            
            // 订阅 Profile 切换事件
            ProfileManager.Instance.ProfileChanged += OnProfileChanged;
            
            // 初始化鼠标检测（如果当前 Profile 启用了）
            InitializeCursorDetection();
            
            // 窗口关闭时清理
            Closing += (s, e) =>
            {
                // 保存窗口状态
                SaveWindowState();
                
                // 停止穿透模式定时器
                StopClickThroughTimer();
                
                // 停止鼠标检测
                StopCursorDetection();
                
                // 取消 Profile 事件订阅
                ProfileManager.Instance.ProfileChanged -= OnProfileChanged;
                
                // 取消事件订阅
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                    WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    WebView.CoreWebView2.SourceChanged -= CoreWebView2_SourceChanged;
                    WebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
                }
            };
            
            // 窗口关闭后退出应用
            Closed += (s, e) =>
            {
                System.Windows.Application.Current.Shutdown();
            };
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
            
            // 应用透明度
            _windowOpacity = state.Opacity;
            
            // 确保窗口在屏幕范围内
            var workArea = SystemParameters.WorkArea;
            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
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
            state.Opacity = _windowOpacity;
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
                var options = new CoreWebView2EnvironmentOptions
                {
                    // 允许自动播放媒体（禁用自动播放限制）
                    AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
                };

                // 创建 WebView2 环境，指定 UserDataFolder 以实现 Cookie 持久化
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder,
                    options: options
                );

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

                // 从保存的状态加载 URL 和静音设置
                var state = WindowStateService.Instance.Load();
                
                // 恢复静音状态
                WebView.CoreWebView2.IsMuted = state.IsMuted;
                
                // 应用透明度
                if (_windowOpacity < AppConstants.MaxOpacity)
                {
                    Win32Helper.SetWindowOpacity(this, _windowOpacity);
                }
                
                // 导航到上次访问的页面（如果有）
                var urlToLoad = !string.IsNullOrWhiteSpace(state.LastUrl) 
                    ? state.LastUrl 
                    : AppConstants.DefaultHomeUrl;
                WebView.CoreWebView2.Navigate(urlToLoad);
            }
            catch (Exception ex)
            {
                // async void 方法必须在内部处理所有异常
                MessageBox.Show(
                    $"WebView2 初始化失败：{ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 处理来自网页的消息
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            
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
                    {
                        Win32Helper.StartMove(this);
                    });
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
                if (!string.IsNullOrWhiteSpace(url) && 
                    !url.StartsWith("about:") &&
                    !url.StartsWith("data:"))
                {
                    DataService.Instance.AddHistory(url, title);
                }
            }
        }

        /// <summary>
        /// URL 变化事件处理
        /// </summary>
        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            // 触发 URL 变化事件
            var currentUrl = WebView.CoreWebView2?.Source ?? string.Empty;
            UrlChanged?.Invoke(this, currentUrl);

            // 触发导航状态变化事件
            NavigationStateChanged?.Invoke(this, EventArgs.Empty);
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
        private void ToggleMaximize()
        {
            if (!_isMaximized)
            {
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
            }
        }

        #endregion

        #region Public Properties

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
            if (WebView.CoreWebView2 == null) return;

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
            if (WebView.CoreWebView2 == null) return;

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
            if (_isClickThrough)
            {
                // 穿透模式下：修改保存的透明度设置
                _opacityBeforeClickThrough = Math.Max(AppConstants.MinOpacity, 
                    _opacityBeforeClickThrough - AppConstants.OpacityStep);
                return _opacityBeforeClickThrough;
            }

            // 非穿透模式：直接修改当前透明度
            _windowOpacity = Math.Max(AppConstants.MinOpacity, _windowOpacity - AppConstants.OpacityStep);
            Win32Helper.SetWindowOpacity(this, _windowOpacity);
            return _windowOpacity;
        }

        /// <summary>
        /// 增加透明度
        /// </summary>
        /// <returns>当前透明度</returns>
        public double IncreaseOpacity()
        {
            if (_isClickThrough)
            {
                // 穿透模式下：修改保存的透明度设置
                _opacityBeforeClickThrough = Math.Min(AppConstants.MaxOpacity, 
                    _opacityBeforeClickThrough + AppConstants.OpacityStep);
                return _opacityBeforeClickThrough;
            }

            // 非穿透模式：直接修改当前透明度
            _windowOpacity = Math.Min(AppConstants.MaxOpacity, _windowOpacity + AppConstants.OpacityStep);
            Win32Helper.SetWindowOpacity(this, _windowOpacity);
            return _windowOpacity;
        }

        /// <summary>
        /// 切换鼠标穿透模式
        /// </summary>
        /// <returns>是否处于穿透模式</returns>
        public bool ToggleClickThrough()
        {
            _isClickThrough = !_isClickThrough;

            if (_isClickThrough)
            {
                // 保存当前透明度
                _opacityBeforeClickThrough = _windowOpacity;
                
                // 启动定时器检测鼠标位置
                StartClickThroughTimer();
            }
            else
            {
                // 停止定时器
                StopClickThroughTimer();
                
                // 恢复之前的透明度
                _windowOpacity = _opacityBeforeClickThrough;
                Win32Helper.SetWindowOpacity(this, _windowOpacity);
            }

            Win32Helper.SetClickThrough(this, _isClickThrough);
            return _isClickThrough;
        }

        /// <summary>
        /// 启动穿透模式鼠标检测定时器
        /// </summary>
        private void StartClickThroughTimer()
        {
            _clickThroughTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _clickThroughTimer.Tick += ClickThroughTimer_Tick;
            _clickThroughTimer.Start();
            
            // 立即检测一次
            UpdateClickThroughOpacity();
        }

        /// <summary>
        /// 停止穿透模式鼠标检测定时器
        /// </summary>
        private void StopClickThroughTimer()
        {
            if (_clickThroughTimer != null)
            {
                _clickThroughTimer.Stop();
                _clickThroughTimer.Tick -= ClickThroughTimer_Tick;
                _clickThroughTimer = null;
            }
            _isCursorInWindowWhileClickThrough = false;
        }

        /// <summary>
        /// 定时器回调：检测鼠标位置并更新透明度
        /// </summary>
        private void ClickThroughTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClickThroughOpacity();
        }

        /// <summary>
        /// 更新穿透模式下的透明度
        /// </summary>
        private void UpdateClickThroughOpacity()
        {
            bool cursorInWindow = Win32Helper.IsCursorInWindow(this);
            
            // 只有状态变化时才更新透明度
            if (cursorInWindow != _isCursorInWindowWhileClickThrough)
            {
                _isCursorInWindowWhileClickThrough = cursorInWindow;
                
                if (cursorInWindow)
                {
                    // 鼠标进入窗口，降至最低透明度
                    Win32Helper.SetWindowOpacity(this, AppConstants.MinOpacity);
                }
                else
                {
                    // 鼠标离开窗口，恢复正常透明度
                    Win32Helper.SetWindowOpacity(this, _opacityBeforeClickThrough);
                }
            }
        }

        /// <summary>
        /// 获取当前透明度百分比
        /// 穿透模式下返回保存的透明度设置，非穿透模式下返回当前窗口透明度
        /// </summary>
        public int OpacityPercent => _isClickThrough 
            ? (int)(_opacityBeforeClickThrough * 100)
            : (int)(_windowOpacity * 100);

        /// <summary>
        /// 是否处于鼠标穿透模式
        /// </summary>
        public bool IsClickThrough => _isClickThrough;

        /// <summary>
        /// 更新配置
        /// </summary>
        /// <param name="config">新配置</param>
        public void UpdateConfig(AppConfig config)
        {
            _config = config;
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
        }

        /// <summary>
        /// 窗口消息处理钩子 - 用于实现边缘吸附
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Win32Helper.WM_ENTERSIZEMOVE:
                    // 开始拖动：记录鼠标相对窗口的偏移
                    if (Win32Helper.GetCursorPosition(out var cursorPos) &&
                        Win32Helper.GetWindowRectangle(hwnd, out var windowRect))
                    {
                        _dragOffset.X = cursorPos.X - windowRect.Left;
                        _dragOffset.Y = cursorPos.Y - windowRect.Top;
                        _isDragging = true;
                    }
                    break;

                case Win32Helper.WM_EXITSIZEMOVE:
                    // 结束拖动
                    _isDragging = false;
                    break;

                case Win32Helper.WM_MOVING:
                    if (_isDragging && lParam != IntPtr.Zero)
                    {
                        HandleWindowMoving(hwnd, lParam);
                    }
                    break;

                case Win32Helper.WM_SIZING:
                    if (lParam != IntPtr.Zero)
                    {
                        HandleWindowSizing(wParam, lParam);
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 处理窗口移动时的边缘吸附
        /// </summary>
        private void HandleWindowMoving(IntPtr hwnd, IntPtr lParam)
        {
            // 获取当前鼠标位置
            if (!Win32Helper.GetCursorPosition(out var cursorPos))
                return;

            // 获取 DPI 缩放比例
            var source = PresentationSource.FromVisual(this);
            double dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

            // 计算物理像素阈值（使用配置值）
            int snapThreshold = _config.EnableEdgeSnap ? _config.SnapThreshold : 0;
            int threshold = (int)(snapThreshold * dpiScale);

            // 获取工作区（物理像素）- 排除任务栏
            var workAreaWpf = SystemParameters.WorkArea;
            var workArea = Win32Helper.ToPhysicalRect(workAreaWpf, dpiScale);

            // 获取屏幕完整区域（物理像素）- 包括任务栏
            var screenRect = new Win32Helper.RECT
            {
                Left = 0,
                Top = 0,
                Right = (int)(SystemParameters.PrimaryScreenWidth * dpiScale),
                Bottom = (int)(SystemParameters.PrimaryScreenHeight * dpiScale)
            };

            // 获取窗口当前大小
            var rect = Marshal.PtrToStructure<Win32Helper.RECT>(lParam);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // 根据鼠标位置和偏移计算用户意图的窗口位置
            int intendedLeft = cursorPos.X - _dragOffset.X;
            int intendedTop = cursorPos.Y - _dragOffset.Y;

            // 对意图位置进行吸附计算
            int finalLeft = intendedLeft;
            int finalTop = intendedTop;

            // 左边缘吸附（工作区和屏幕边缘相同）
            if (Math.Abs(intendedLeft - workArea.Left) <= threshold)
            {
                finalLeft = workArea.Left;
            }
            // 右边缘吸附（工作区和屏幕边缘相同）
            else if (Math.Abs(intendedLeft + width - workArea.Right) <= threshold)
            {
                finalLeft = workArea.Right - width;
            }

            // 上边缘吸附（工作区）
            if (Math.Abs(intendedTop - workArea.Top) <= threshold)
            {
                finalTop = workArea.Top;
            }
            // 下边缘吸附 - 优先工作区（任务栏上方）
            else if (Math.Abs(intendedTop + height - workArea.Bottom) <= threshold)
            {
                finalTop = workArea.Bottom - height;
            }
            // 下边缘吸附 - 屏幕真实底部
            else if (Math.Abs(intendedTop + height - screenRect.Bottom) <= threshold)
            {
                finalTop = screenRect.Bottom - height;
            }

            // 更新窗口位置
            rect.Left = finalLeft;
            rect.Top = finalTop;
            rect.Right = finalLeft + width;
            rect.Bottom = finalTop + height;

            Marshal.StructureToPtr(rect, lParam, false);
        }

        /// <summary>
        /// 处理窗口调整大小时的边缘吸附
        /// </summary>
        private void HandleWindowSizing(IntPtr wParam, IntPtr lParam)
        {
            // 获取 DPI 缩放比例
            var source = PresentationSource.FromVisual(this);
            double dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

            // 计算物理像素阈值（使用配置值）
            int snapThreshold = _config.EnableEdgeSnap ? _config.SnapThreshold : 0;
            int threshold = (int)(snapThreshold * dpiScale);

            // 获取工作区（物理像素）
            var workAreaWpf = SystemParameters.WorkArea;
            var workArea = Win32Helper.ToPhysicalRect(workAreaWpf, dpiScale);

            int sizingEdge = wParam.ToInt32();
            var rect = Marshal.PtrToStructure<Win32Helper.RECT>(lParam);
            Win32Helper.SnapSizingEdge(ref rect, workArea, threshold, sizingEdge);
            Marshal.StructureToPtr(rect, lParam, false);
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

            Cursor = direction switch
            {
                Win32Helper.ResizeDirection.Left => Cursors.SizeWE,
                Win32Helper.ResizeDirection.Right => Cursors.SizeWE,
                Win32Helper.ResizeDirection.Top => Cursors.SizeNS,
                Win32Helper.ResizeDirection.Bottom => Cursors.SizeNS,
                Win32Helper.ResizeDirection.TopLeft => Cursors.SizeNWSE,
                Win32Helper.ResizeDirection.BottomRight => Cursors.SizeNWSE,
                Win32Helper.ResizeDirection.TopRight => Cursors.SizeNESW,
                Win32Helper.ResizeDirection.BottomLeft => Cursors.SizeNESW,
                _ => Cursors.Arrow
            };
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
                _windowOpacity = _opacityBeforeCursorDetection;
                Win32Helper.SetWindowOpacity(this, _windowOpacity);
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
                if (_isClickThrough)
                    return;

                // 保存当前透明度并降低
                if (!_isOpacityReducedByCursorDetection)
                {
                    _opacityBeforeCursorDetection = _windowOpacity;
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
                if (_isClickThrough)
                    return;

                // 恢复之前的透明度
                if (_isOpacityReducedByCursorDetection)
                {
                    _windowOpacity = _opacityBeforeCursorDetection;
                    Win32Helper.SetWindowOpacity(this, _windowOpacity);
                    _isOpacityReducedByCursorDetection = false;
                }
            });
        }

        #endregion
    }
}
