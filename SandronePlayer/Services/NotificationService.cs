using System;
using System.Threading.Tasks;
using System.Windows;
using SandronePlayer.Models;
using SandronePlayer.Views;

namespace SandronePlayer.Services
{
    /// <summary>
    /// 通知服务（单例）
    /// 负责管理和显示应用内通知，替代系统原生 MessageBox
    /// </summary>
    public class NotificationService
    {
        #region Singleton

        private static NotificationService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static NotificationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new NotificationService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Constructor

        private NotificationService()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 显示通知（自动关闭）
        /// </summary>
        /// <param name="message">通知消息</param>
        /// <param name="type">通知类型</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="durationMs">显示持续时间（毫秒），默认 3000ms</param>
        public void Show(string message, NotificationType type = NotificationType.Info,
                        string? title = null, int durationMs = 3000)
        {
            try
            {
                // 确保在 UI 线程上执行
                if (Application.Current?.Dispatcher == null)
                {
                    LogService.Instance.Warn("NotificationService", "无法显示通知：Application.Current 为空");
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var config = new NotificationConfig
                    {
                        Message = message,
                        Title = title,
                        Type = type,
                        DurationMs = durationMs
                    };

                    var window = new NotificationWindow(config);
                    CenterWindowOnScreen(window);
                    window.Show();
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("NotificationService", $"显示通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <returns>用户选择：true=确定，false=取消</returns>
        public async Task<bool> ConfirmAsync(string message, string? title = null)
        {
            var result = await ShowDialogAsync(message, title, "确定", "取消");
            return result == true;
        }

        /// <summary>
        /// 显示带自定义按钮的对话框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题（可选）</param>
        /// <param name="yesText">确定按钮文本</param>
        /// <param name="noText">取消按钮文本</param>
        /// <param name="showCancel">是否显示取消按钮（暂未实现三按钮模式）</param>
        /// <returns>用户选择：true=确定，false=取消，null=关闭按钮</returns>
        public Task<bool?> ShowDialogAsync(string message, string? title = null,
                                           string yesText = "确定", string noText = "取消",
                                           bool showCancel = false)
        {
            var tcs = new TaskCompletionSource<bool?>();

            try
            {
                // 确保在 UI 线程上执行
                if (Application.Current?.Dispatcher == null)
                {
                    LogService.Instance.Warn("NotificationService", "无法显示对话框：Application.Current 为空");
                    tcs.SetResult(false);
                    return tcs.Task;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new ConfirmDialog(message, title, yesText, noText);
                    CenterWindowOnScreen(dialog);
                    
                    dialog.Closed += (s, e) =>
                    {
                        tcs.TrySetResult(dialog.Result);
                    };

                    dialog.Show();
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("NotificationService", $"显示对话框失败: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 将窗口居中显示在主屏幕上
        /// </summary>
        /// <param name="window">要居中的窗口</param>
        private static void CenterWindowOnScreen(Window window)
        {
            // 获取主屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 窗口需要先测量才能获取实际尺寸
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            
            // 使用 SizeToContent 时，需要在 Loaded 事件中调整位置
            // 这里先设置一个初始位置，然后在 Loaded 事件中调整
            window.Loaded += (s, e) =>
            {
                window.Left = (workArea.Width - window.ActualWidth) / 2 + workArea.Left;
                window.Top = (workArea.Height - window.ActualHeight) / 2 + workArea.Top;
            };
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// 显示信息通知
        /// </summary>
        public void Info(string message, string? title = null, int durationMs = 3000)
            => Show(message, NotificationType.Info, title, durationMs);

        /// <summary>
        /// 显示成功通知
        /// </summary>
        public void Success(string message, string? title = null, int durationMs = 3000)
            => Show(message, NotificationType.Success, title, durationMs);

        /// <summary>
        /// 显示警告通知
        /// </summary>
        public void Warning(string message, string? title = null, int durationMs = 3000)
            => Show(message, NotificationType.Warning, title, durationMs);

        /// <summary>
        /// 显示错误通知
        /// </summary>
        public void Error(string message, string? title = null, int durationMs = 4000)
            => Show(message, NotificationType.Error, title, durationMs);

        #endregion
    }
}
