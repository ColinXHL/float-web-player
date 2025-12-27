using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Core
{
    /// <summary>
    /// 依赖注入容器配置扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 配置应用程序的所有服务
        /// </summary>
        public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
        {
            // TODO: 步骤 2 完成后取消注释
            // 基础服务（无依赖）
            // services.AddSingleton<ILogService, LogService>();

            // 核心服务
            // services.AddSingleton<IConfigService, ConfigService>();
            // services.AddSingleton<IDataService, DataService>();

            // 业务服务
            // services.AddSingleton<IProfileManager, ProfileManager>();
            // services.AddSingleton<IPluginHost, PluginHost>();

            // 功能服务
            // services.AddSingleton<ISubtitleService, SubtitleService>();
            // services.AddSingleton<INotificationService, NotificationService>();
            // services.AddSingleton<IWindowStateService, WindowStateService>();
            // services.AddSingleton<ICursorDetectionService, CursorDetectionService>();
            // services.AddSingleton<IPioneerNoteService, PioneerNoteService>();

            // 插件系统
            // services.AddSingleton<IOverlayManager, OverlayManager>();
            // services.AddSingleton<IPluginAssociationManager, PluginAssociationManager>();
            // services.AddSingleton<ISubscriptionManager, SubscriptionManager>();

            // 注册表服务
            // services.AddSingleton<IPluginRegistry, PluginRegistry>();
            // services.AddSingleton<IProfileRegistry, ProfileRegistry>();

            // 窗口（瞬态，每次请求创建新实例）
            // services.AddTransient<PlayerWindow>();
            // services.AddTransient<ControlBarWindow>();

            return services;
        }
    }
}
