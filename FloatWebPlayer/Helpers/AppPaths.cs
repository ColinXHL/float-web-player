using System;
using System.IO;
using System.Reflection;

namespace FloatWebPlayer.Helpers
{
    /// <summary>
    /// 应用程序路径管理（静态类）
    /// 所有路径统一从这里获取
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// 应用程序根目录（exe 所在目录）
        /// </summary>
        public static string AppDirectory { get; }

        /// <summary>
        /// 用户数据根目录（User/）
        /// </summary>
        public static string UserDirectory { get; }

        /// <summary>
        /// WebView2 数据目录（User/WebView2Data/）
        /// 存放浏览器缓存、Cookie 等
        /// </summary>
        public static string WebView2DataDirectory { get; }

        /// <summary>
        /// 配置数据目录（User/Data/）
        /// 存放 config.json、Profiles 等
        /// </summary>
        public static string DataDirectory { get; }

        /// <summary>
        /// Profiles 目录（User/Data/Profiles/）
        /// </summary>
        public static string ProfilesDirectory { get; }

        /// <summary>
        /// 配置文件路径（User/Data/config.json）
        /// </summary>
        public static string ConfigFilePath { get; }

        /// <summary>
        /// 内置 Profile 目录（exe 同级的 Profiles/）
        /// 存放随软件分发的 Profile 模板，只读
        /// </summary>
        public static string BuiltInProfilesDirectory { get; }

        /// <summary>
        /// 内置插件目录（exe 同级的 Plugins/）
        /// 存放随软件分发的插件源码，只读
        /// </summary>
        public static string BuiltInPluginsDirectory { get; }

        /// <summary>
        /// 订阅配置文件路径（User/Data/subscriptions.json）
        /// 存放用户的 Profile 和插件订阅信息
        /// </summary>
        public static string SubscriptionsFilePath { get; }

        static AppPaths()
        {
            // 获取应用程序目录
            AppDirectory = AppContext.BaseDirectory;

            // 用户数据目录：应用目录/User/
            UserDirectory = Path.Combine(AppDirectory, "User");

            // WebView2 数据目录
            WebView2DataDirectory = Path.Combine(UserDirectory, "WebView2Data");

            // 配置数据目录
            DataDirectory = Path.Combine(UserDirectory, "Data");

            // Profiles 目录
            ProfilesDirectory = Path.Combine(DataDirectory, "Profiles");

            // 配置文件路径
            ConfigFilePath = Path.Combine(DataDirectory, "config.json");

            // 内置 Profile 目录（exe 同级的 Profiles/）
            BuiltInProfilesDirectory = Path.Combine(AppDirectory, "Profiles");

            // 内置插件目录（exe 同级的 Plugins/）
            BuiltInPluginsDirectory = Path.Combine(AppDirectory, "Plugins");

            // 订阅配置文件路径
            SubscriptionsFilePath = Path.Combine(DataDirectory, "subscriptions.json");

            // 确保目录存在
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// 确保所有必要目录存在
        /// </summary>
        private static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(UserDirectory);
            Directory.CreateDirectory(WebView2DataDirectory);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(ProfilesDirectory);
        }
    }
}
