using System.Reflection;

namespace AkashaNavigator
{
/// <summary>
/// 应用程序常量定义
/// 集中管理所有配置常量，便于后续设置窗口引用和用户自定义
/// </summary>
public static class AppConstants
{
#region Application Info

    /// <summary>
    /// 应用程序版本
    /// </summary>
    public static readonly string Version = GetCurrentVersion();

    private static string GetCurrentVersion()
    {
        var assembly = typeof(AppConstants).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadataIndex = informational.IndexOf('+');
            return metadataIndex > 0
                ? informational[..metadataIndex]
                : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

#endregion

#region Window - PlayerWindow

    /// <summary>
    /// 窗口最小宽度
    /// </summary>
    public const double MinWindowWidth = 200;

    /// <summary>
    /// 窗口最小高度
    /// </summary>
    public const double MinWindowHeight = 150;

    /// <summary>
    /// 拖拽边框厚度（像素）
    /// </summary>
    public const int ResizeBorderThickness = 8;

    /// <summary>
    /// 缩放提示悬停触发延迟（毫秒）
    /// </summary>
    public const int ResizeHintHoverDelayMs = 500;

    /// <summary>
    /// 按住 Shift 时的缩放宽高比（宽）
    /// </summary>
    public const double AspectRatio16By9Width = 16.0;

    /// <summary>
    /// 按住 Shift 时的缩放宽高比（高）
    /// </summary>
    public const double AspectRatio16By9Height = 9.0;

    /// <summary>
    /// 缩放提示消息
    /// </summary>
    public const string ResizeHintMessage = "按住 Shift 可按 16:9 缩放";

    /// <summary>
    /// 缩放提示图标
    /// </summary>
    public const string ResizeHintIcon = "📐";

    /// <summary>
    /// 边缘吸附阈值（像素）
    /// </summary>
    public const int SnapThreshold = 15;

    /// <summary>
    /// 边缘中点吸附阈值（像素）
    /// </summary>
    public const int CenterSnapThreshold = 30;

    /// <summary>
    /// 边缘中点吸附释放滞后（像素）
    /// </summary>
    public const int CenterSnapHysteresis = 8;

#endregion

#region Opacity

    /// <summary>
    /// 最小透明度
    /// </summary>
    public const double MinOpacity = 0.2;

    /// <summary>
    /// 最大透明度
    /// </summary>
    public const double MaxOpacity = 1.0;

    /// <summary>
    /// 透明度步进
    /// </summary>
    public const double OpacityStep = 0.1;

    /// <summary>
    /// 默认窥视透明度
    /// </summary>
    public const double DefaultPeekOpacity = 0.2;

#endregion

#region Video Control

    /// <summary>
    /// 默认快进/倒退秒数
    /// </summary>
    public const int DefaultSeekSeconds = 5;

    /// <summary>
    /// 视频时间同步间隔（毫秒）
    /// </summary>
    public const int VideoTimeSyncIntervalMs = 400;

    /// <summary>
    /// 视频时间同步在拥塞时的退避间隔（毫秒）
    /// </summary>
    public const int VideoTimeSyncBackoffIntervalMs = 800;

    /// <summary>
    /// 脚本队列拥塞阈值（达到后跳过本次时间同步）
    /// </summary>
    public const int VideoTimeSyncQueueBackpressureThreshold = 2;

    /// <summary>
    /// 向插件广播 timeUpdate 的最小间隔（毫秒）
    /// </summary>
    public const int PluginTimeUpdateMinIntervalMs = 300;

    /// <summary>
    /// timeUpdate 强制广播阈值（秒，超过该跳变值时无视限频）
    /// </summary>
    public const double PluginTimeUpdateForceDeltaSeconds = 1.0;

    /// <summary>
    /// 插件事件回调慢调用告警阈值（毫秒）
    /// </summary>
    public const int PluginEventSlowCallbackWarnMs = 30;

#endregion

#region OSD

    /// <summary>
    /// OSD 淡入动画时长（毫秒）
    /// </summary>
    public const int OsdFadeInDuration = 200;

    /// <summary>
    /// OSD 显示停留时长（毫秒）
    /// </summary>
    public const int OsdDisplayDuration = 1000;

    /// <summary>
    /// OSD 淡出动画时长（毫秒）
    /// </summary>
    public const int OsdFadeOutDuration = 300;

#endregion

#region ControlBar

    /// <summary>
    /// 控制栏展开高度
    /// </summary>
    public const double ControlBarExpandedHeight = 55;

    /// <summary>
    /// 控制栏触发线高度
    /// </summary>
    public const double ControlBarTriggerLineHeight = 16;

    /// <summary>
    /// 控制栏位置算法版本
    /// </summary>
    public const int ControlBarPositionVersion = 2;

    /// <summary>
    /// 屏幕顶部触发区域比例
    /// </summary>
    public const double ControlBarTriggerAreaRatio = 1.0 / 4.0;

    /// <summary>
    /// 延迟隐藏时间（毫秒）
    /// </summary>
    public const int ControlBarHideDelayMs = 400;

    /// <summary>
    /// 状态切换防抖时间（毫秒）
    /// </summary>
    public const int ControlBarStateStabilityMs = 150;

#endregion

#region URLs

    /// <summary>
    /// 默认首页 URL
    /// </summary>
    public const string DefaultHomeUrl = "https://www.bilibili.com";

#endregion

#region Updater

    /// <summary>
    /// 更新程序文件名
    /// </summary>
    public const string UpdaterFileName = "AkashaNavigator.update.exe";

#endregion

#region File Names

    /// <summary>
    /// Profile 配置文件名
    /// </summary>
    public const string ProfileFileName = "profile.json";

    /// <summary>
    /// 应用配置文件名
    /// </summary>
    public const string ConfigFileName = "config.json";

    /// <summary>
    /// 历史记录文件名
    /// </summary>
    public const string HistoryFileName = "history.json";

    /// <summary>
    /// 书签文件名
    /// </summary>
    public const string BookmarksFileName = "bookmarks.json";

    /// <summary>
    /// 窗口状态文件名
    /// </summary>
    public const string WindowStateFileName = "window_state.json";

    /// <summary>
    /// 插件清单文件名
    /// </summary>
    public const string PluginManifestFileName = "plugin.json";

    /// <summary>
    /// 未在插件清单中显式声明时使用的设置界面文件名。
    /// </summary>
    public const string PluginSettingsUiFileName = "settings_ui.json";

    /// <summary>
    /// 插件仓库索引文件名。
    /// </summary>
    public const string PluginRepositoryIndexFileName = "repo.json";

    /// <summary>
    /// 插件仓库配置文件名。
    /// </summary>
    public const string PluginRepositoriesConfigFileName = "plugin-repositories.json";

    public const string PluginRepositorySubscriptionsFileName =
        "plugin-repository-subscriptions.json";

    public const string PluginRepositoryManifestFileName = "manifest.json";

    public const string PluginRepositoryResourcesFileName = "resources.json";

    /// <summary>
    /// 插件配置文件名
    /// </summary>
    public const string PluginConfigFileName = "config.json";

    /// <summary>
    /// 开荒笔记数据文件名
    /// </summary>
    public const string PioneerNotesFileName = "pioneer-notes.json";

#endregion

#region IDs and Names

    /// <summary>
    /// Akasha Automation 插件 ID。
    /// </summary>
    public const string AutomationPluginId = "akasha-genshin-automation";

    /// <summary>
    /// 官方插件仓库 ID。
    /// </summary>
    public const string OfficialPluginRepositoryId = "official";

    /// <summary>
    /// 官方插件仓库默认分支。
    /// </summary>
    public const string OfficialPluginRepositoryBranch = "catalog";

    /// <summary>
    /// 官方插件仓库 GitHub 地址。
    /// </summary>
    public const string OfficialPluginRepositoryGitHubUrl =
        "https://github.com/ColinXHL/akasha-plugins.git";

    /// <summary>
    /// 官方插件仓库 CNB 地址。
    /// </summary>
    public const string OfficialPluginRepositoryCnbUrl =
        "https://cnb.cool/AkashaNavigator/akasha-plugins.git";

    public const string OfficialPluginReleaseGitHubUrlFormat =
        "https://github.com/ColinXHL/akasha-plugins/releases/download/{0}/{1}";

    public const string OfficialPluginReleaseCnbUrlFormat =
        "https://cnb.cool/AkashaNavigator/akasha-plugins/-/releases/download/{0}/{1}";

    public const string PluginDistributionRepository = "repository";

    public const string PluginDistributionRelease = "release";

    public const string PluginInstallSourceRepository = "repository";

    public const string PluginInstallSourceBuiltIn = "builtin";

    public const string PluginInstallSourceExternal = "external";

    public const string PluginInstallSourceMigrated = "migrated";

    public const string CompanionBackendType = "companion-process";

    public const string CompanionLifetimePlugin = "plugin";

    public const string CompanionIntegrityLevelInherit = "inherit";

    public const int CompanionProtocolVersion = 1;

    public const int DefaultCompanionShutdownTimeoutMs = 5000;

    public const int MaxCompanionShutdownTimeoutMs = 60000;

    /// <summary>
    /// 传给插件伴生进程的用户资源目录环境变量。
    /// </summary>
    public const string PluginDataDirectoryEnvironmentVariable = "AKASHA_PLUGIN_DATA_DIR";

    /// <summary>
    /// 默认 Profile ID
    /// </summary>
    public const string DefaultProfileId = "default";

    /// <summary>
    /// 默认 Profile 名称
    /// </summary>
    public const string DefaultProfileName = "Default";

#endregion

#region Directory Names

    /// <summary>
    /// 插件目录名
    /// </summary>
    public const string PluginsDirectoryName = "plugins";

#endregion
}
}
