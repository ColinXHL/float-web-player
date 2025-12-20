using System;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AkashaNavigator.Helpers
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
    /// Repos 根目录（exe 同级的 Repos/）
    /// 存放内置插件和 Profile 模板
    /// </summary>
    public static string ReposDirectory { get; }

    /// <summary>
    /// 内置 Profile 目录（exe 同级的 Repos/Profiles/）
    /// 存放随软件分发的 Profile 模板，只读
    /// </summary>
    public static string BuiltInProfilesDirectory { get; }

    /// <summary>
    /// 内置插件目录（exe 同级的 Repos/Plugins/）
    /// 存放随软件分发的插件源码，只读
    /// </summary>
    public static string BuiltInPluginsDirectory { get; }

    /// <summary>
    /// 订阅配置文件路径（User/Data/subscriptions.json）
    /// 存放用户的 Profile 和插件订阅信息
    /// </summary>
    public static string SubscriptionsFilePath { get; }

    /// <summary>
    /// 全局插件库目录（User/Data/InstalledPlugins/）
    /// 存放所有已安装插件的本体文件
    /// </summary>
    public static string InstalledPluginsDirectory { get; }

    /// <summary>
    /// 插件库索引文件路径（User/Data/library.json）
    /// 存放全局插件库的索引信息
    /// </summary>
    public static string LibraryIndexPath { get; }

    /// <summary>
    /// 插件-Profile关联索引文件路径（User/Data/associations.json）
    /// 存放插件与Profile的映射关系
    /// </summary>
    public static string AssociationsFilePath { get; }

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

        // Repos 根目录
        ReposDirectory = Path.Combine(AppDirectory, "Repos");

        // 内置 Profile 目录（exe 同级的 Repos/Profiles/）
        BuiltInProfilesDirectory = Path.Combine(ReposDirectory, "Profiles");

        // 内置插件目录（exe 同级的 Repos/Plugins/）
        BuiltInPluginsDirectory = Path.Combine(ReposDirectory, "Plugins");

        // 订阅配置文件路径
        SubscriptionsFilePath = Path.Combine(DataDirectory, "subscriptions.json");

        // 全局插件库目录
        InstalledPluginsDirectory = Path.Combine(DataDirectory, "InstalledPlugins");

        // 插件库索引文件路径
        LibraryIndexPath = Path.Combine(DataDirectory, "library.json");

        // 插件-Profile关联索引文件路径
        AssociationsFilePath = Path.Combine(DataDirectory, "associations.json");

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
        Directory.CreateDirectory(InstalledPluginsDirectory);

        // 为 WebView2 数据文件夹设置写权限
        EnsureWebView2FolderPermissions();
    }

    /// <summary>
    /// 确保 WebView2 数据文件夹具有写权限
    /// 解决某些环境下 WebView2 无法写入缓存/Cookie 的问题
    /// </summary>
    private static void EnsureWebView2FolderPermissions()
    {
        try
        {
            var info = new DirectoryInfo(WebView2DataDirectory);
            var access = info.GetAccessControl();
            access.AddAccessRule(
                new FileSystemAccessRule("Everyone", FileSystemRights.FullControl,
                                         InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                         PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(access);
        }
        catch
        {
            // 权限设置失败时静默处理，不影响程序启动
            // 可能是普通用户权限不足，WebView2 仍可能正常工作
        }
    }
}
}
