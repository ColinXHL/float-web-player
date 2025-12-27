using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Views.Dialogs;
using AkashaNavigator.Core;
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

    private Bootstrapper? _bootstrapper;
    private PlayerWindow? _playerWindow;
    private ControlBarWindow? _controlBarWindow;
    private HotkeyService? _hotkeyService;
    private OsdWindow? _osdWindow;
    private AppConfig _config = null!;

    /// <summary>
    /// 日志级别开关，用于运行时动态切换日志级别
    /// </summary>
    private static readonly LoggingLevelSwitch _logLevelSwitch = new(LogEventLevel.Information);

    // ✅ 新增：注入的服务字段
    private IConfigService? _configService;
    private INotificationService? _notificationService;
    private DataMigration? _dataMigration;
    private IPluginHost? _pluginHost;
    private PluginLibrary? _pluginLibrary;

#endregion

#region Properties

    /// <summary>
    /// 全局服务提供者，用于在需要时获取DI容器中的服务
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

#endregion

#region Event Handlers

    /// <summary>
    /// 应用启动事件
    /// </summary>
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 配置 Serilog 日志系统
        ConfigureSerilog();

        // 初始化 DI 容器和服务
        _bootstrapper = new Bootstrapper();
        var serviceProvider = _bootstrapper.GetServiceProvider();

        // 保存ServiceProvider供全局访问
        Services = serviceProvider;

        // 触发 LogService 初始化
        var logService = serviceProvider.GetRequiredService<ILogService>();

        // 执行数据迁移
        ExecuteDataMigration();

        // 从 DI 容器获取配置服务
        _configService = serviceProvider.GetRequiredService<IConfigService>();
        _config = _configService.Config;

        // ✅ 注入其他需要的服务
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _dataMigration = serviceProvider.GetRequiredService<DataMigration>();
        _pluginHost = serviceProvider.GetRequiredService<IPluginHost>();
        _pluginLibrary = serviceProvider.GetRequiredService<PluginLibrary>();

        // 根据配置更新日志级别
        UpdateLogLevel();

        // 首次启动显示欢迎弹窗
        if (_config.IsFirstLaunch)
        {
            var welcomeDialog = new WelcomeDialog();
            welcomeDialog.ShowDialog();

            // 标记为非首次启动并保存
            _config.IsFirstLaunch = false;
            _configService.Save();
        }

        // 订阅配置变更事件
        _configService.ConfigChanged += (s, config) =>
        {
            _config = config;
            ApplySettings();
        };

        // 使用 Bootstrapper 创建窗口并启动应用（包括窗口绑定和插件加载）
        _bootstrapper.Run();

        // 获取窗口引用（用于快捷键服务和插件更新检查）
        _playerWindow = serviceProvider.GetRequiredService<PlayerWindow>();

        // 启动全局快捷键服务
        StartHotkeyService();

        // 设置插件更新检查
        SetupPluginUpdateCheck();
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
            if (_pluginLibrary == null || _notificationService == null)
                return;

            var updates = _pluginLibrary.CheckAllUpdates();
            if (updates.Count == 0)
                return;

            var dialogFactory = Services.GetRequiredService<IDialogFactory>();
            var dialog = dialogFactory.CreatePluginUpdatePromptDialog(updates);
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
                        var updateResult = _pluginLibrary.UpdatePlugin(update.PluginId);
                        if (updateResult.IsSuccess)
                            successCount++;
                        else
                            failCount++;
                    }

                    // 显示更新结果
                    if (failCount == 0)
                    {
                        _notificationService.Success($"成功更新 {successCount} 个插件！", "更新完成");
                    }
                    else
                    {
                        _notificationService.Warning($"更新完成：{successCount} 个成功，{failCount} 个失败。",
                                                             "更新完成");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            var logService = _bootstrapper?.GetServiceProvider().GetRequiredService<ILogService>();
            logService?.Error("App", ex, "检查插件更新时发生异常");
        }
    }

    /// <summary>
    /// 执行数据迁移
    /// </summary>
    private void ExecuteDataMigration()
    {
        try
        {
            var logService = _bootstrapper?.GetServiceProvider().GetRequiredService<ILogService>();

            if (_dataMigration == null || logService == null)
                return;

            if (!_dataMigration.NeedsMigration())
            {
                return;
            }

            logService.Info("App", "检测到需要数据迁移，开始执行...");

            var result = _dataMigration.Migrate();

            switch (result.Status)
            {
            case MigrationResultStatus.Success:
                logService.Info(
                    "App", "数据迁移成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                    result.MigratedPluginCount, result.MigratedProfileCount);
                break;

            case MigrationResultStatus.PartialSuccess:
                logService.Warn(
                    "App", "数据迁移部分成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                    result.MigratedPluginCount, result.MigratedProfileCount);
                foreach (var warning in result.Warnings)
                {
                    logService.Warn("App", "迁移警告: {Warning}", warning);
                }
                break;

            case MigrationResultStatus.Failed:
                logService.Error("App", "数据迁移失败: {ErrorMessage}", result.ErrorMessage);
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
            var logService = _bootstrapper?.GetServiceProvider().GetRequiredService<ILogService>();
            logService?.Error("App", ex, "数据迁移过程中发生异常");
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
        _pluginHost?.UnloadAllPlugins();

        // 关闭并刷新 Serilog 日志
        Log.CloseAndFlush();

        base.OnExit(e);
    }

#endregion
}
}
