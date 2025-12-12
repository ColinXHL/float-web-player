using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
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

        #region Constants

        /// <summary>
        /// 拖拽边框厚度（像素）
        /// </summary>
        private const int ResizeBorderThickness = 8;

        /// <summary>
        /// 窗口最小宽度
        /// </summary>
        private const double MinWindowWidth = 200;

        /// <summary>
        /// 窗口最小高度
        /// </summary>
        private const double MinWindowHeight = 150;

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

        #endregion

        #region Constructor

        public PlayerWindow()
        {
            InitializeComponent();
            InitializeWindowPosition();
            InitializeWebView();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化窗口位置和大小
        /// 默认位置：屏幕左下角
        /// 默认大小：屏幕大小的 1/16
        /// </summary>
        private void InitializeWindowPosition()
        {
            // 获取主屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 计算默认大小：屏幕大小的 1/16
            // 1/16 = 1/4 宽度 x 1/4 高度
            Width = Math.Max(workArea.Width / 4, MinWindowWidth);
            Height = Math.Max(workArea.Height / 4, MinWindowHeight);

            // 定位到屏幕左下角
            Left = workArea.Left;
            Top = workArea.Bottom - Height;
        }

        #endregion

        #region WebView2 Initialization

        /// <summary>
        /// 获取 WebView2 UserDataFolder 路径
        /// 用于持久化 Cookie 和其他用户数据
        /// </summary>
        private static string GetUserDataFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FloatWebPlayer",
                "WebView2Data"
            );
        }

        /// <summary>
        /// 初始化 WebView2 控件
        /// </summary>
        private async void InitializeWebView()
        {
            try
            {
                var userDataFolder = GetUserDataFolder();
                
                // 确保目录存在
                Directory.CreateDirectory(userDataFolder);

                // 创建 WebView2 环境，指定 UserDataFolder 以实现 Cookie 持久化
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder
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

                // 导航到默认页面（B站）
                WebView.CoreWebView2.Navigate("https://www.bilibili.com");
            }
            catch (Exception ex)
            {
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
                    WindowState = WindowState.Minimized;
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

        #endregion

        #region Event Handlers

        /// <summary>
        /// 窗口源初始化完成
        /// </summary>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 预留：后续可添加其他初始化逻辑
        }

        /// <summary>
        /// 鼠标左键按下：边框区域调整大小，其他区域拖动窗口
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            var position = e.GetPosition(this);
            var direction = Win32Helper.GetResizeDirection(this, position, ResizeBorderThickness);

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
            var direction = Win32Helper.GetResizeDirection(this, position, ResizeBorderThickness);

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
    }
}
