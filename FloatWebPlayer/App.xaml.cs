using System.Windows;
using FloatWebPlayer.Views;

namespace FloatWebPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        #region Fields

        private PlayerWindow? _playerWindow;
        private ControlBarWindow? _controlBarWindow;

        #endregion

        #region Event Handlers

        /// <summary>
        /// 应用启动事件
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 创建主窗口（播放器）
            _playerWindow = new PlayerWindow();

            // 创建控制栏窗口
            _controlBarWindow = new ControlBarWindow();

            // 设置窗口间事件关联
            SetupWindowBindings();

            // 显示窗口
            _playerWindow.Show();
            _controlBarWindow.Show();
        }

        /// <summary>
        /// 设置两窗口之间的事件绑定
        /// </summary>
        private void SetupWindowBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 控制栏导航请求 → 播放器窗口加载
            _controlBarWindow.NavigateRequested += (s, url) =>
            {
                _playerWindow.Navigate(url);
            };

            // 控制栏后退请求
            _controlBarWindow.BackRequested += (s, e) =>
            {
                _playerWindow.GoBack();
            };

            // 控制栏前进请求
            _controlBarWindow.ForwardRequested += (s, e) =>
            {
                _playerWindow.GoForward();
            };

            // 控制栏刷新请求
            _controlBarWindow.RefreshRequested += (s, e) =>
            {
                _playerWindow.Refresh();
            };

            // 播放器窗口关闭时，关闭控制栏
            _playerWindow.Closed += (s, e) =>
            {
                _controlBarWindow.Close();
            };

            // 播放器 URL 变化时，同步到控制栏
            _playerWindow.UrlChanged += (s, url) =>
            {
                _controlBarWindow.CurrentUrl = url;
            };

            // 播放器导航状态变化时，更新控制栏按钮
            _playerWindow.NavigationStateChanged += (s, e) =>
            {
                _controlBarWindow.UpdateBackButtonState(_playerWindow.CanGoBack);
                _controlBarWindow.UpdateForwardButtonState(_playerWindow.CanGoForward);
            };
        }

        #endregion
    }
}

