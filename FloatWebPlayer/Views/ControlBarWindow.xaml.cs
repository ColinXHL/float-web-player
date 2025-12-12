using System;
using System.Windows;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// ControlBarWindow - URL 控制栏窗口
    /// </summary>
    public partial class ControlBarWindow : Window
    {
        #region Events

        /// <summary>
        /// 导航请求事件
        /// </summary>
        public event EventHandler<string>? NavigateRequested;

        /// <summary>
        /// 后退请求事件
        /// </summary>
        public event EventHandler? BackRequested;

        /// <summary>
        /// 前进请求事件
        /// </summary>
        public event EventHandler? ForwardRequested;

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// 收藏请求事件
        /// </summary>
        public event EventHandler? BookmarkRequested;

        /// <summary>
        /// 菜单请求事件
        /// </summary>
        public event EventHandler? MenuRequested;

        #endregion

        #region Fields

        /// <summary>
        /// 是否正在拖动
        /// </summary>
        private bool _isDragging;

        /// <summary>
        /// 拖动起始点的 X 坐标（屏幕坐标）
        /// </summary>
        private double _dragStartX;

        /// <summary>
        /// 拖动起始时窗口的 Left 值
        /// </summary>
        private double _windowStartLeft;

        #endregion

        #region Constructor

        public ControlBarWindow()
        {
            InitializeComponent();
            InitializeWindowPosition();
        }

        #endregion

        #region Properties

        /// <summary>
        /// 获取或设置当前 URL
        /// </summary>
        public string CurrentUrl
        {
            get => UrlTextBox.Text;
            set => UrlTextBox.Text = value;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 初始化窗口位置和大小
        /// 位置：屏幕顶部，水平居中
        /// 宽度：屏幕宽度的 1/3
        /// </summary>
        private void InitializeWindowPosition()
        {
            // 获取主屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 计算宽度：屏幕宽度的 1/3，最小 400px
            Width = Math.Max(workArea.Width / 3, 400);

            // 水平居中
            Left = workArea.Left + (workArea.Width - Width) / 2;

            // 顶部定位（留 2px 边距）
            Top = workArea.Top + 2;
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
        /// 拖动条鼠标按下：开始拖动
        /// </summary>
        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _isDragging = true;
                _dragStartX = PointToScreen(e.GetPosition(this)).X;
                _windowStartLeft = Left;

                // 捕获鼠标
                Mouse.Capture(DragBar);

                // 注册鼠标移动和释放事件
                DragBar.MouseMove += DragBar_MouseMove;
                DragBar.MouseLeftButtonUp += DragBar_MouseLeftButtonUp;
            }
        }

        /// <summary>
        /// 拖动条鼠标移动：执行水平拖动
        /// </summary>
        private void DragBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentX = PointToScreen(e.GetPosition(this)).X;
                var deltaX = currentX - _dragStartX;

                // 计算新位置
                var newLeft = _windowStartLeft + deltaX;

                // 限制在屏幕范围内
                var workArea = SystemParameters.WorkArea;
                newLeft = Math.Max(workArea.Left, Math.Min(newLeft, workArea.Right - Width));

                Left = newLeft;
            }
        }

        /// <summary>
        /// 拖动条鼠标释放：结束拖动
        /// </summary>
        private void DragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;

                // 释放鼠标捕获
                Mouse.Capture(null);

                // 取消事件注册
                DragBar.MouseMove -= DragBar_MouseMove;
                DragBar.MouseLeftButtonUp -= DragBar_MouseLeftButtonUp;
            }
        }

        /// <summary>
        /// URL 地址栏按键事件
        /// </summary>
        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var url = UrlTextBox.Text.Trim();
                
                if (!string.IsNullOrEmpty(url))
                {
                    // 自动补全 URL scheme
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + url;
                    }

                    NavigateRequested?.Invoke(this, url);
                }

                // 移除焦点
                Keyboard.ClearFocus();
            }
        }

        /// <summary>
        /// 前往按钮点击
        /// </summary>
        private void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            
            if (!string.IsNullOrEmpty(url))
            {
                // 自动补全 URL scheme
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                NavigateRequested?.Invoke(this, url);
            }

            // 移除焦点
            Keyboard.ClearFocus();
        }

        /// <summary>
        /// 后退按钮点击
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 前进按钮点击
        /// </summary>
        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            ForwardRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 收藏按钮点击
        /// </summary>
        private void BtnBookmark_Click(object sender, RoutedEventArgs e)
        {
            BookmarkRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 菜单按钮点击
        /// </summary>
        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 更新后退按钮状态
        /// </summary>
        public void UpdateBackButtonState(bool canGoBack)
        {
            BtnBack.IsEnabled = canGoBack;
        }

        /// <summary>
        /// 更新前进按钮状态
        /// </summary>
        public void UpdateForwardButtonState(bool canGoForward)
        {
            BtnForward.IsEnabled = canGoForward;
        }

        /// <summary>
        /// 更新收藏按钮状态（是否已收藏）
        /// </summary>
        public void UpdateBookmarkState(bool isBookmarked)
        {
            // 更新收藏按钮图标
            var textBlock = BtnBookmark.Content as System.Windows.Controls.TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = isBookmarked ? "★" : "☆";
            }
        }

        #endregion
    }
}
