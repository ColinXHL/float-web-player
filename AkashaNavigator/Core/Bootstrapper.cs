using Microsoft.Extensions.DependencyInjection;
using System;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Plugins.Core;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 应用程序启动引导器
    /// 负责初始化 DI 容器和应用程序核心组件
    /// </summary>
    public class Bootstrapper
    {
        private readonly IServiceProvider _serviceProvider;
        private PlayerWindow? _playerWindow;
        private ControlBarWindow? _controlBarWindow;

        public Bootstrapper()
        {
            // 配置服务并构建 DI 容器
            var services = new ServiceCollection();
            services.ConfigureAppServices();
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// 启动应用程序
        /// </summary>
        public void Run()
        {
            var sp = _serviceProvider;

            // 从 DI 容器获取主窗口
            _playerWindow = sp.GetRequiredService<PlayerWindow>();

            // 设置 PluginApi 的全局窗口获取器（在创建 PlayerWindow 后立即设置）
            PluginApi.SetGlobalWindowGetter(() => _playerWindow);

            // 加载当前 Profile 的插件
            var profileManager = sp.GetRequiredService<IProfileManager>();
            var pluginHost = sp.GetRequiredService<IPluginHost>();
            var currentProfileId = profileManager.CurrentProfile?.Id ?? "";
            pluginHost.LoadPluginsForProfile(currentProfileId);

            // 手动创建 ControlBarWindow（依赖 PlayerWindow）
            _controlBarWindow = new ControlBarWindow(_playerWindow);

            // 设置窗口间事件绑定
            SetupWindowBindings();

            // 显示主窗口
            _playerWindow.Show();

            // 启动控制栏自动显示/隐藏
            _controlBarWindow.StartAutoShowHide();
        }

        /// <summary>
        /// 设置窗口间事件绑定
        /// </summary>
        private void SetupWindowBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            SetupNavigationBindings();
            SetupPlayerBindings();
            SetupMenuBindings();
            SetupBookmarkBindings();
        }

        /// <summary>
        /// 设置导航相关事件绑定
        /// 包含导航请求、后退、前进、刷新事件
        /// </summary>
        private void SetupNavigationBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 控制栏导航请求 → 播放器窗口加载
            _controlBarWindow.NavigateRequested += (s, url) =>
            { _playerWindow.Navigate(url); };

            // 控制栏后退请求
            _controlBarWindow.BackRequested += (s, e) =>
            { _playerWindow.GoBack(); };

            // 控制栏前进请求
            _controlBarWindow.ForwardRequested += (s, e) =>
            { _playerWindow.GoForward(); };

            // 控制栏刷新请求
            _controlBarWindow.RefreshRequested += (s, e) =>
            { _playerWindow.Refresh(); };
        }

        /// <summary>
        /// 设置播放器窗口相关事件绑定
        /// 包含窗口关闭、URL 变化、导航状态变化事件
        /// </summary>
        private void SetupPlayerBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 播放器窗口关闭时，关闭控制栏并退出应用
            _playerWindow.Closed += (s, e) =>
            {
                _controlBarWindow.Close();
                System.Windows.Application.Current.Shutdown();
            };

            // 播放器 URL 变化时，同步到控制栏
            _playerWindow.UrlChanged += (s, url) =>
            { _controlBarWindow.CurrentUrl = url; };

            // 播放器导航状态变化时，更新控制栏按钮
            _playerWindow.NavigationStateChanged += (s, e) =>
            {
                _controlBarWindow.UpdateBackButtonState(_playerWindow.CanGoBack);
                _controlBarWindow.UpdateForwardButtonState(_playerWindow.CanGoForward);
            };

            // 播放器 URL 变化时，检查收藏状态
            _playerWindow.UrlChanged += (s, url) =>
            {
                var dataService = _serviceProvider.GetRequiredService<IDataService>();
                var isBookmarked = dataService.IsBookmarked(url);
                _controlBarWindow.UpdateBookmarkState(isBookmarked);
            };
        }

        /// <summary>
        /// 设置菜单相关事件绑定
        /// 包含历史记录、收藏夹、插件中心、设置、归档菜单事件
        /// </summary>
        private void SetupMenuBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 历史记录菜单事件
            _controlBarWindow.HistoryRequested += (s, e) =>
            {
                var historyWindow = _serviceProvider.GetRequiredService<HistoryWindow>();
                historyWindow.HistoryItemSelected += (sender, url) =>
                { _playerWindow.Navigate(url); };
                historyWindow.ShowDialog();
            };

            // 收藏夹菜单事件
            _controlBarWindow.BookmarksRequested += (s, e) =>
            {
                var bookmarkPopup = _serviceProvider.GetRequiredService<BookmarkPopup>();
                bookmarkPopup.BookmarkItemSelected += (sender, url) =>
                { _playerWindow.Navigate(url); };
                bookmarkPopup.ShowDialog();
            };

            // 插件中心菜单事件
            _controlBarWindow.PluginCenterRequested += (s, e) =>
            {
                var pluginCenterWindow = new PluginCenterWindow();
                // 设置 Owner 为 PlayerWindow，确保插件中心显示在 PlayerWindow 之上
                pluginCenterWindow.Owner = _playerWindow;
                pluginCenterWindow.ShowDialog();
            };

            // 设置菜单事件
            _controlBarWindow.SettingsRequested += (s, e) =>
            {
                var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                // 设置 Owner 为 PlayerWindow，确保设置窗口显示在 PlayerWindow 之上
                settingsWindow.Owner = _playerWindow;
                settingsWindow.ShowDialog();
            };

            // 记录笔记按钮点击事件
            _controlBarWindow.RecordNoteRequested += (s, e) =>
            {
                var url = _controlBarWindow.CurrentUrl;
                var title = _playerWindow.CurrentTitle;
                var recordDialogFactory = _serviceProvider.GetRequiredService<Func<string, string, RecordNoteDialog>>();
                var recordDialog = recordDialogFactory(url, title);
                recordDialog.Owner = _playerWindow;
                recordDialog.ShowDialog();
            };

            // 开荒笔记菜单事件
            _controlBarWindow.PioneerNotesRequested += (s, e) =>
            {
                var noteWindow = _serviceProvider.GetRequiredService<PioneerNoteWindow>();
                noteWindow.NoteItemSelected += (sender, url) =>
                { _playerWindow.Navigate(url); };
                noteWindow.Owner = _playerWindow;
                noteWindow.ShowDialog();
            };
        }

        /// <summary>
        /// 设置收藏按钮相关事件绑定
        /// </summary>
        private void SetupBookmarkBindings()
        {
            if (_playerWindow == null || _controlBarWindow == null)
                return;

            // 收藏按钮点击事件
            _controlBarWindow.BookmarkRequested += (s, e) =>
            {
                var url = _controlBarWindow.CurrentUrl;
                var title = _playerWindow.CurrentTitle;
                var dataService = _serviceProvider.GetRequiredService<IDataService>();
                var isBookmarked = dataService.ToggleBookmark(url, title);
                _controlBarWindow.UpdateBookmarkState(isBookmarked);
            };
        }

        /// <summary>
        /// 获取服务提供者（用于需要手动解析服务的场景）
        /// </summary>
        public IServiceProvider GetServiceProvider()
        {
            return _serviceProvider;
        }
    }
}
