using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// NotificationWindow - 自定义通知窗口
    /// 继承 AnimatedWindow，提供淡入淡出动画效果
    /// </summary>
    public partial class NotificationWindow : AnimatedWindow
    {
        #region Fields

        private readonly DispatcherTimer _autoCloseTimer;
        private readonly NotificationConfig _config;

        #endregion

        #region Constructor

        public NotificationWindow(NotificationConfig config)
        {
            InitializeComponent();
            _config = config;

            // 设置自动关闭定时器
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(config.DurationMs)
            };
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;

            // 应用配置
            ApplyConfig();

            // 窗口加载后启动定时器
            Loaded += (s, e) => _autoCloseTimer.Start();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 应用通知配置
        /// </summary>
        private void ApplyConfig()
        {
            // 设置消息
            MessageText.Text = _config.Message;

            // 设置标题（可选）
            if (!string.IsNullOrWhiteSpace(_config.Title))
            {
                TitleText.Text = _config.Title;
                TitleText.Visibility = Visibility.Visible;
            }

            // 应用通知类型的视觉样式
            ApplyNotificationTypeStyle(_config.Type);
        }

        /// <summary>
        /// 根据通知类型应用视觉样式
        /// </summary>
        private void ApplyNotificationTypeStyle(NotificationType type)
        {
            var (color, icon) = GetTypeVisuals(type);
            
            AccentBorder.Background = new SolidColorBrush(color);
            IconText.Text = icon;
            IconText.Foreground = new SolidColorBrush(color);
        }

        /// <summary>
        /// 获取通知类型对应的颜色和图标
        /// </summary>
        public static (Color color, string icon) GetTypeVisuals(NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => (Color.FromRgb(0x00, 0x78, 0xD4), "ℹ"),      // 蓝色 #0078D4
                NotificationType.Success => (Color.FromRgb(0x10, 0x7C, 0x10), "✓"),   // 绿色 #107C10
                NotificationType.Warning => (Color.FromRgb(0xFF, 0x8C, 0x00), "⚠"),   // 橙色 #FF8C00
                NotificationType.Error => (Color.FromRgb(0xE8, 0x11, 0x23), "✕"),     // 红色 #E81123
                _ => (Color.FromRgb(0x00, 0x78, 0xD4), "ℹ")
            };
        }

        /// <summary>
        /// 自动关闭定时器触发
        /// </summary>
        private void AutoCloseTimer_Tick(object? sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            CloseWithAnimation();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            CloseWithAnimation();
        }

        #endregion

        #region Protected Overrides

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer.Stop();
            base.OnClosed(e);
        }

        #endregion
    }
}
