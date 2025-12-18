using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SandronePlayer.Views
{
    /// <summary>
    /// OSD 操作提示窗口
    /// 显示快捷键操作的半透明提示，自动淡出
    /// </summary>
    public partial class OsdWindow : Window
    {

        #region Fields

        /// <summary>
        /// 自动隐藏定时器
        /// </summary>
        private DispatcherTimer? _hideTimer;

        /// <summary>
        /// 当前是否正在显示
        /// </summary>
        private bool _isShowing;

        #endregion

        #region Constructor

        public OsdWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 显示 OSD 提示
        /// </summary>
        /// <param name="message">提示文字</param>
        /// <param name="icon">图标（可选）</param>
        public void ShowMessage(string message, string? icon = null)
        {
            // 停止之前的隐藏定时器
            StopHideTimer();

            // 更新内容
            MessageText.Text = message;
            IconText.Text = icon ?? string.Empty;
            IconText.Visibility = string.IsNullOrEmpty(icon) ? Visibility.Collapsed : Visibility.Visible;

            // 先确保窗口已显示（Opacity=0 不可见）
            if (!IsVisible)
            {
                Opacity = 0;
                Show();
            }

            // 强制更新布局以获取正确的 ActualWidth/Height
            UpdateLayout();

            // 定位到屏幕中央
            CenterOnScreen();

            // 淡入动画
            FadeIn();

            // 设置定时隐藏
            StartHideTimer();

            _isShowing = true;
        }

        /// <summary>
        /// 隐藏 OSD
        /// </summary>
        public void HideMessage()
        {
            if (!_isShowing) return;

            StopHideTimer();
            FadeOut();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 将窗口定位到屏幕中央
        /// </summary>
        private void CenterOnScreen()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            Left = (screenWidth - ActualWidth) / 2;
            Top = (screenHeight - ActualHeight) / 2;
        }

        /// <summary>
        /// 淡入动画
        /// </summary>
        private void FadeIn()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(AppConstants.OsdFadeInDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, animation);
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private void FadeOut()
        {
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(AppConstants.OsdFadeOutDuration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            animation.Completed += (s, e) =>
            {
                Hide();
                _isShowing = false;
            };

            BeginAnimation(OpacityProperty, animation);
        }

        /// <summary>
        /// 启动自动隐藏定时器
        /// </summary>
        private void StartHideTimer()
        {
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AppConstants.OsdDisplayDuration)
            };
            _hideTimer.Tick += HideTimer_Tick;
            _hideTimer.Start();
        }

        /// <summary>
        /// 停止自动隐藏定时器
        /// </summary>
        private void StopHideTimer()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer.Tick -= HideTimer_Tick;
                _hideTimer = null;
            }
        }

        /// <summary>
        /// 定时器回调：开始淡出
        /// </summary>
        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            StopHideTimer();
            FadeOut();
        }

        #endregion
    }
}
