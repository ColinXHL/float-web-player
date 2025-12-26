using System;
using System.IO;
using System.Windows;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Dialogs;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AkashaNavigator
{
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
#region Fields

    private PlayerWindow? _playerWindow;
    private ControlBarWindow? _controlBarWindow;
    private HotkeyService? _hotkeyService;
    private OsdWindow? _osdWindow;
    private AppConfig _config = null!;

    /// <summary>
    /// 日志级别开关，用于运行时动态切换日志级别
    /// </summary>
    private static readonly LoggingLevelSwitch _logLevelSwitch = new(LogEventLevel.Information);

#endregion

#region Event Handlers

    /// <summary>
    /// 应用启动事件
    /// </summary>
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 配置 Serilog 日志系统
        ConfigureSerilog();

        // 执行数据迁移（如果需要）
        ExecuteDataMigration();

        // 初始化服务（单例）
        _ = ProfileManager.Instance;
        _ = DataService.Instance;

        // 加载配置
        _config = ConfigService.Instance.Config;

        // 根据配置更新日志级别
        UpdateLogLevel();

        // 首次启动显示欢迎弹窗
        if (_config.IsFirstLaunch)
        {
            var welcomeDialog = new WelcomeDialog();
            welcomeDialog.ShowDialog();

            // 标记为非首次启动并保存
            _config.IsFirstLaunch = false;
            ConfigService.Instance.Save();
        }

        // 订阅配置变更事件
        ConfigService.Instance.ConfigChanged += (s, config) =>
        {
            _config = config;
            ApplySettings();
        };

        // 创建主窗口（播放器）
        _playerWindow = new PlayerWindow();

        // 设置 PluginApi 的全局窗口获取器（在创建 PlayerWindow 后立即设置）
        PluginApi.SetGlobalWindowGetter(() => _playerWindow);

        // 加载当前 Profile 的插件
        var currentProfileId = ProfileManager.Instance.CurrentProfile.Id;
        PluginHost.Instance.LoadPluginsForProfile(currentProfileId);

        // 创建控制栏窗口
        _controlBarWindow = new ControlBarWindow();

        // 设置窗口间事件关联
        SetupWindowBindings();

        // 显示窗口
        _playerWindow.Show();

        // 控制栏窗口启动自动显示/隐藏监听（默认隐藏，鼠标移到顶部触发显示）
        _controlBarWindow.StartAutoShowHide();

        // 启动全局快捷键服务
        StartHotkeyService();
    }

    /// <summary>
    /// 设置两窗口之间的事件绑定
    /// </summary>
    private void SetupWindowBindings()
    {
        if (_playerWindow == null || _controlBarWindow == null)
            return;

        SetupNavigationBindings();
        SetupPlayerBindings();
        SetupMenuBindings();
        SetupBookmarkBindings();
        SetupPluginUpdateCheck();
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
            Shutdown();
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
            var isBookmarked = DataService.Instance.IsBookmarked(url);
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
            var historyWindow = new HistoryWindow();
            historyWindow.HistoryItemSelected += (sender, url) =>
            { _playerWindow.Navigate(url); };
            historyWindow.ShowDialog();
        };

        // 收藏夹菜单事件
        _controlBarWindow.BookmarksRequested += (s, e) =>
        {
            var bookmarkPopup = new BookmarkPopup();
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
            var settingsWindow = new SettingsWindow();
            // 设置 Owner 为 PlayerWindow，确保设置窗口显示在 PlayerWindow 之上
            settingsWindow.Owner = _playerWindow;
            settingsWindow.ShowDialog();
        };

        // 记录笔记按钮点击事件
        _controlBarWindow.RecordNoteRequested += (s, e) =>
        {
            var url = _controlBarWindow.CurrentUrl;
            var title = _playerWindow.CurrentTitle;
            var recordDialog = new RecordNoteDialog(url, title);
            recordDialog.Owner = _playerWindow;
            recordDialog.ShowDialog();
            if (recordDialog.Result)
            {
                ShowOsd("已记录", "💾");
            }
        };

        // 开荒笔记菜单事件
        _controlBarWindow.PioneerNotesRequested += (s, e) =>
        {
            var noteWindow = new PioneerNoteWindow();
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
            var isBookmarked = DataService.Instance.ToggleBookmark(url, title);
            _controlBarWindow.UpdateBookmarkState(isBookmarked);
            ShowOsd(isBookmarked ? "已添加收藏" : "已取消收藏", "⭐");
        };
    }

    /// <summary>
    /// 设置插件更新检查
    /// WebView 首次加载完成后检查插件更新（非首次启动且启用了更新提示）
    /// </summary>
    private void SetupPluginUpdateCheck()
    {
        if (_playerWindow == null)
            return;

        if (!_config.IsFirstLaunch && _config.EnablePluginUpdateNotification)
        {
            // 使用一次性事件处理器
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                _playerWindow.NavigationStateChanged -= handler;
                // 延迟一小段时间再显示，确保窗口完全加载
                Dispatcher.BeginInvoke(new Action(CheckAndPromptPluginUpdates),
                                       System.Windows.Threading.DispatcherPriority.Background);
            };
            _playerWindow.NavigationStateChanged += handler;
        }
    }

    /// <summary>
    /// 启动全局快捷键服务
    /// </summary>
    private void StartHotkeyService()
    {
        _hotkeyService = new HotkeyService();

        // 使用 AppConfig 中的快捷键配置初始化
        _hotkeyService.UpdateConfig(_config.ToHotkeyConfig());

        // 绑定快捷键事件
        _hotkeyService.SeekBackward += (s, e) =>
        {
            var seconds = _config.SeekSeconds;
            _playerWindow?.SeekAsync(-seconds);
            ShowOsd($"-{seconds}s", "⏪");
        };

        _hotkeyService.SeekForward += (s, e) =>
        {
            var seconds = _config.SeekSeconds;
            _playerWindow?.SeekAsync(seconds);
            ShowOsd($"+{seconds}s", "⏩");
        };

        _hotkeyService.TogglePlay += (s, e) =>
        {
            _playerWindow?.TogglePlayAsync();
            ShowOsd("播放/暂停", "⏯");
        };

        _hotkeyService.DecreaseOpacity += (s, e) =>
        {
            var opacity = _playerWindow?.DecreaseOpacity();
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔅");
            }
        };

        _hotkeyService.IncreaseOpacity += (s, e) =>
        {
            var opacity = _playerWindow?.IncreaseOpacity();
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔆");
            }
        };

        _hotkeyService.ToggleClickThrough += (s, e) =>
        {
            // 最大化时禁用穿透热键
            if (_playerWindow?.IsMaximized == true)
                return;

            var isClickThrough = _playerWindow?.ToggleClickThrough();
            if (isClickThrough.HasValue)
            {
                var msg = isClickThrough.Value ? "鼠标穿透已开启" : "鼠标穿透已关闭";
                ShowOsd(msg, "👆");
            }
        };

        _hotkeyService.ToggleMaximize += (s, e) =>
        {
            _playerWindow?.ToggleMaximize();
            var msg = _playerWindow?.IsMaximized == true ? "窗口: 最大化" : "窗口: 还原";
            ShowOsd(msg, "🔲");
        };

        _hotkeyService.Start();
    }

    /// <summary>
    /// 显示 OSD 提示
    /// </summary>
    /// <param name="message">提示文字</param>
    /// <param name="icon">图标（可选）</param>
    private void ShowOsd(string message, string? icon = null)
    {
        // 延迟初始化 OSD 窗口
        _osdWindow ??= new OsdWindow();
        _osdWindow.ShowMessage(message, icon);
    }

    /// <summary>
    /// 应用设置变更
    /// </summary>
    private void ApplySettings()
    {
        // 更新日志级别
        UpdateLogLevel();

        // 更新 PlayerWindow 配置
        _playerWindow?.UpdateConfig(_config);

        // 更新 HotkeyService 配置
        if (_hotkeyService != null)
        {
            _hotkeyService.UpdateConfig(_config.ToHotkeyConfig());
        }
    }

    /// <summary>
    /// 配置 Serilog 日志系统
    /// </summary>
    private void ConfigureSerilog()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var logFile = Path.Combine(logDirectory, "akasha-navigator-.log");

        Log.Logger =
            new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_logLevelSwitch)
                .WriteTo
                .File(logFile,
                      outputTemplate: ("[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] " +
                                       "[{SourceContext}]{NewLine}{Message}{NewLine}{Exception}{NewLine}"),
                      rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31,
                      retainedFileTimeLimit: TimeSpan.FromDays(21))
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

        Log.Information("Serilog 日志系统已初始化");
    }

    /// <summary>
    /// 根据配置更新日志级别
    /// </summary>
    private void UpdateLogLevel()
    {
        var newLevel = _config.EnableDebugLog ? LogEventLevel.Debug : LogEventLevel.Information;
        if (_logLevelSwitch.MinimumLevel != newLevel)
        {
            _logLevelSwitch.MinimumLevel = newLevel;
            Log.Information("日志级别已切换为 {Level}", newLevel);
        }
    }

    /// <summary>
    /// 检查并提示插件更新
    /// </summary>
    private void CheckAndPromptPluginUpdates()
    {
        try
        {
            var updates = PluginLibrary.Instance.CheckAllUpdates();
            if (updates.Count == 0)
                return;

            var dialog = new PluginUpdatePromptDialog(updates);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                switch (dialog.Result)
                {
                case PluginUpdatePromptResult.OpenPluginCenter:
                    // 延迟打开插件中心（等待主窗口创建完成）
                    Dispatcher.BeginInvoke(new Action(() =>
                                                      {
                                                          if (_playerWindow != null)
                                                          {
                                                              var pluginCenterWindow = new PluginCenterWindow();
                                                              pluginCenterWindow.Owner = _playerWindow;
                                                              // 导航到已安装插件页面
                                                              pluginCenterWindow.NavigateToInstalledPlugins();
                                                              pluginCenterWindow.ShowDialog();
                                                          }
                                                      }),
                                           System.Windows.Threading.DispatcherPriority.Loaded);
                    break;

                case PluginUpdatePromptResult.UpdateAll:
                    // 执行一键更新
                    var successCount = 0;
                    var failCount = 0;
                    foreach (var update in updates)
                    {
                        var updateResult = PluginLibrary.Instance.UpdatePlugin(update.PluginId);
                        if (updateResult.IsSuccess)
                            successCount++;
                        else
                            failCount++;
                    }

                    // 显示更新结果
                    if (failCount == 0)
                    {
                        NotificationService.Instance.Success($"成功更新 {successCount} 个插件！", "更新完成");
                    }
                    else
                    {
                        NotificationService.Instance.Warning($"更新完成：{successCount} 个成功，{failCount} 个失败。",
                                                             "更新完成");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("App", ex, "检查插件更新时发生异常");
        }
    }

    /// <summary>
    /// 执行数据迁移
    /// </summary>
    private void ExecuteDataMigration()
    {
        try
        {
            if (!DataMigration.Instance.NeedsMigration())
            {
                return;
            }

            LogService.Instance.Info("App", "检测到需要数据迁移，开始执行...");

            var result = DataMigration.Instance.Migrate();

            switch (result.Status)
            {
            case MigrationResultStatus.Success:
                LogService.Instance.Info(
                    "App", "数据迁移成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                    result.MigratedPluginCount, result.MigratedProfileCount);
                break;

            case MigrationResultStatus.PartialSuccess:
                LogService.Instance.Warn(
                    "App", "数据迁移部分成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                    result.MigratedPluginCount, result.MigratedProfileCount);
                foreach (var warning in result.Warnings)
                {
                    LogService.Instance.Warn("App", "迁移警告: {Warning}", warning);
                }
                break;

            case MigrationResultStatus.Failed:
                LogService.Instance.Error("App", "数据迁移失败: {ErrorMessage}", result.ErrorMessage);
                MessageBox.Show($"数据迁移失败：{result.ErrorMessage}\n\n应用将继续运行，但部分插件可能无法正常工作。",
                                "迁移警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                break;

            case MigrationResultStatus.NotNeeded:
                // 无需迁移，静默处理
                break;
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("App", ex, "数据迁移过程中发生异常");
            // 不阻止应用启动，只记录错误
        }
    }

    /// <summary>
    /// 应用退出事件
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        // 先停止快捷键服务
        _hotkeyService?.Dispose();

        // 确保控制栏停止定时器
        _controlBarWindow?.StopAutoShowHide();

        // 卸载所有插件
        PluginHost.Instance.UnloadAllPlugins();

        // 关闭并刷新 Serilog 日志
        Log.CloseAndFlush();

        base.OnExit(e);
    }

#endregion
}
}
