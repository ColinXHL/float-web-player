using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    #region Result Types

    /// <summary>
    /// 插件安装结果状态
    /// </summary>
    public enum InstallResultStatus
    {
        /// <summary>安装成功</summary>
        Success,
        /// <summary>插件已安装</summary>
        AlreadyInstalled,
        /// <summary>源目录不存在</summary>
        SourceNotFound,
        /// <summary>清单无效</summary>
        InvalidManifest,
        /// <summary>文件复制失败</summary>
        CopyFailed
    }

    /// <summary>
    /// 插件安装结果
    /// </summary>
    public class InstallResult
    {
        public InstallResultStatus Status { get; private set; }
        public string? ErrorMessage { get; private set; }
        public InstalledPluginInfo? PluginInfo { get; private set; }

        private InstallResult() { }

        public bool IsSuccess => Status == InstallResultStatus.Success;

        public static InstallResult Success(InstalledPluginInfo pluginInfo) => new()
        {
            Status = InstallResultStatus.Success,
            PluginInfo = pluginInfo
        };

        public static InstallResult AlreadyInstalled(string pluginId) => new()
        {
            Status = InstallResultStatus.AlreadyInstalled,
            ErrorMessage = $"插件 {pluginId} 已安装"
        };

        public static InstallResult SourceNotFound(string path) => new()
        {
            Status = InstallResultStatus.SourceNotFound,
            ErrorMessage = $"源目录不存在: {path}"
        };

        public static InstallResult InvalidManifest(string message) => new()
        {
            Status = InstallResultStatus.InvalidManifest,
            ErrorMessage = $"清单无效: {message}"
        };

        public static InstallResult CopyFailed(string message) => new()
        {
            Status = InstallResultStatus.CopyFailed,
            ErrorMessage = $"文件复制失败: {message}"
        };
    }


    /// <summary>
    /// 插件卸载结果状态
    /// </summary>
    public enum UninstallResultStatus
    {
        /// <summary>卸载成功</summary>
        Success,
        /// <summary>插件未安装</summary>
        NotInstalled,
        /// <summary>插件被引用且未强制卸载</summary>
        HasReferences,
        /// <summary>文件删除失败</summary>
        DeleteFailed
    }

    /// <summary>
    /// 插件卸载结果
    /// </summary>
    public class UninstallResult
    {
        public UninstallResultStatus Status { get; private set; }
        public string? ErrorMessage { get; private set; }
        public List<string>? ReferencingProfiles { get; private set; }

        private UninstallResult() { }

        public bool IsSuccess => Status == UninstallResultStatus.Success;

        public static UninstallResult Success() => new()
        {
            Status = UninstallResultStatus.Success
        };

        public static UninstallResult NotInstalled(string pluginId) => new()
        {
            Status = UninstallResultStatus.NotInstalled,
            ErrorMessage = $"插件 {pluginId} 未安装"
        };

        public static UninstallResult HasReferences(string pluginId, List<string> profiles) => new()
        {
            Status = UninstallResultStatus.HasReferences,
            ErrorMessage = $"插件 {pluginId} 被 {profiles.Count} 个 Profile 引用",
            ReferencingProfiles = profiles
        };

        public static UninstallResult DeleteFailed(string message) => new()
        {
            Status = UninstallResultStatus.DeleteFailed,
            ErrorMessage = $"文件删除失败: {message}"
        };
    }

    #endregion

    #region Event Args

    /// <summary>
    /// 插件库变化类型
    /// </summary>
    public enum PluginLibraryChangeType
    {
        Installed,
        Uninstalled,
        Updated
    }

    /// <summary>
    /// 插件库变化事件参数
    /// </summary>
    public class PluginLibraryChangedEventArgs : EventArgs
    {
        public PluginLibraryChangeType ChangeType { get; }
        public string PluginId { get; }
        public InstalledPluginInfo? PluginInfo { get; }

        public PluginLibraryChangedEventArgs(PluginLibraryChangeType changeType, string pluginId, InstalledPluginInfo? pluginInfo = null)
        {
            ChangeType = changeType;
            PluginId = pluginId;
            PluginInfo = pluginInfo;
        }
    }

    #endregion


    /// <summary>
    /// 全局插件库管理服务
    /// 负责插件本体的安装、卸载、更新
    /// </summary>
    public class PluginLibrary
    {
        #region Singleton

        private static PluginLibrary? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static PluginLibrary Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PluginLibrary();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 重置单例（仅用于测试）
        /// </summary>
        internal static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// 全局插件库目录
        /// </summary>
        public string LibraryDirectory => AppPaths.InstalledPluginsDirectory;

        /// <summary>
        /// 插件库索引文件路径
        /// </summary>
        public string LibraryIndexPath => AppPaths.LibraryIndexPath;

        #endregion

        #region Fields

        private PluginLibraryIndex _index;
        private readonly object _indexLock = new();

        #endregion

        #region Constructor

        private PluginLibrary()
        {
            _index = LoadIndex();
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        internal PluginLibrary(string libraryDirectory, string indexPath)
        {
            // 此构造函数用于测试，允许自定义路径
            _index = PluginLibraryIndex.LoadFromFile(indexPath);
        }

        #endregion

        #region Index Management

        /// <summary>
        /// 加载索引文件
        /// </summary>
        private PluginLibraryIndex LoadIndex()
        {
            return PluginLibraryIndex.LoadFromFile(LibraryIndexPath);
        }

        /// <summary>
        /// 保存索引文件
        /// </summary>
        private void SaveIndex()
        {
            lock (_indexLock)
            {
                _index.SaveToFile(LibraryIndexPath);
            }
        }

        /// <summary>
        /// 重新加载索引
        /// </summary>
        public void ReloadIndex()
        {
            lock (_indexLock)
            {
                _index = LoadIndex();
            }
        }

        #endregion


        #region Query Methods

        /// <summary>
        /// 获取所有已安装插件
        /// </summary>
        public List<InstalledPluginInfo> GetInstalledPlugins()
        {
            lock (_indexLock)
            {
                var result = new List<InstalledPluginInfo>();

                foreach (var entry in _index.Plugins)
                {
                    var manifest = GetPluginManifest(entry.Id);
                    if (manifest != null)
                    {
                        var info = InstalledPluginInfo.FromManifest(manifest, entry.Source);
                        info.InstalledAt = entry.InstalledAt;
                        result.Add(info);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 检查插件是否已安装
        /// </summary>
        public bool IsInstalled(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
                return false;

            lock (_indexLock)
            {
                return _index.Plugins.Any(p => 
                    string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 获取插件目录
        /// </summary>
        public string GetPluginDirectory(string pluginId)
        {
            return Path.Combine(LibraryDirectory, pluginId);
        }

        /// <summary>
        /// 获取插件清单
        /// </summary>
        public PluginManifest? GetPluginManifest(string pluginId)
        {
            var pluginDir = GetPluginDirectory(pluginId);
            var manifestPath = Path.Combine(pluginDir, "plugin.json");

            var result = PluginManifest.LoadFromFile(manifestPath);
            return result.IsSuccess ? result.Manifest : null;
        }

        /// <summary>
        /// 获取已安装插件信息
        /// </summary>
        public InstalledPluginInfo? GetInstalledPluginInfo(string pluginId)
        {
            lock (_indexLock)
            {
                var entry = _index.Plugins.FirstOrDefault(p => 
                    string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                    return null;

                var manifest = GetPluginManifest(pluginId);
                if (manifest == null)
                    return null;

                var info = InstalledPluginInfo.FromManifest(manifest, entry.Source);
                info.InstalledAt = entry.InstalledAt;
                return info;
            }
        }

        #endregion


        #region Install Methods

        /// <summary>
        /// 安装插件到全局库
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="sourceDirectory">源目录（为null时从内置插件目录查找）</param>
        /// <returns>安装结果</returns>
        public InstallResult InstallPlugin(string pluginId, string? sourceDirectory = null)
        {
            // 检查是否已安装
            if (IsInstalled(pluginId))
            {
                return InstallResult.AlreadyInstalled(pluginId);
            }

            // 确定源目录
            var sourcePath = sourceDirectory ?? Path.Combine(AppPaths.BuiltInPluginsDirectory, pluginId);

            // 检查源目录是否存在
            if (!Directory.Exists(sourcePath))
            {
                return InstallResult.SourceNotFound(sourcePath);
            }

            // 检查清单文件
            var manifestPath = Path.Combine(sourcePath, "plugin.json");
            var manifestResult = PluginManifest.LoadFromFile(manifestPath);
            if (!manifestResult.IsSuccess)
            {
                return InstallResult.InvalidManifest(manifestResult.ErrorMessage ?? "未知错误");
            }

            var manifest = manifestResult.Manifest!;

            // 确保插件ID匹配
            if (!string.Equals(manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return InstallResult.InvalidManifest($"清单中的ID ({manifest.Id}) 与请求的ID ({pluginId}) 不匹配");
            }

            // 复制插件文件到全局库
            var targetDir = GetPluginDirectory(pluginId);
            try
            {
                CopyDirectory(sourcePath, targetDir);
            }
            catch (Exception ex)
            {
                return InstallResult.CopyFailed(ex.Message);
            }

            // 更新索引
            var entry = new InstalledPluginEntry
            {
                Id = pluginId,
                Version = manifest.Version ?? "1.0.0",
                InstalledAt = DateTime.Now,
                Source = sourceDirectory == null ? "builtin" : "external"
            };

            lock (_indexLock)
            {
                _index.Plugins.Add(entry);
                SaveIndex();
            }

            // 创建插件信息
            var pluginInfo = InstalledPluginInfo.FromManifest(manifest, entry.Source);
            pluginInfo.InstalledAt = entry.InstalledAt;

            // 触发事件
            OnPluginChanged(new PluginLibraryChangedEventArgs(
                PluginLibraryChangeType.Installed, pluginId, pluginInfo));

            return InstallResult.Success(pluginInfo);
        }

        /// <summary>
        /// 复制目录及其内容
        /// </summary>
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            // 创建目标目录
            Directory.CreateDirectory(targetDir);

            // 复制文件
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            // 递归复制子目录
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(dir, targetSubDir);
            }
        }

        #endregion


        #region Uninstall Methods

        /// <summary>
        /// 卸载插件
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="force">是否强制卸载（忽略引用检查）</param>
        /// <param name="getReferencingProfiles">获取引用该插件的Profile列表的委托（用于解耦）</param>
        /// <returns>卸载结果</returns>
        public UninstallResult UninstallPlugin(string pluginId, bool force = false, 
            Func<string, List<string>>? getReferencingProfiles = null)
        {
            // 检查是否已安装
            if (!IsInstalled(pluginId))
            {
                return UninstallResult.NotInstalled(pluginId);
            }

            // 检查引用（如果提供了委托且不是强制卸载）
            if (!force && getReferencingProfiles != null)
            {
                var profiles = getReferencingProfiles(pluginId);
                if (profiles.Count > 0)
                {
                    return UninstallResult.HasReferences(pluginId, profiles);
                }
            }

            // 删除插件目录
            var pluginDir = GetPluginDirectory(pluginId);
            try
            {
                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, true);
                }
            }
            catch (Exception ex)
            {
                return UninstallResult.DeleteFailed(ex.Message);
            }

            // 更新索引
            lock (_indexLock)
            {
                _index.Plugins.RemoveAll(p => 
                    string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
                SaveIndex();
            }

            // 触发事件
            OnPluginChanged(new PluginLibraryChangedEventArgs(
                PluginLibraryChangeType.Uninstalled, pluginId));

            return UninstallResult.Success();
        }

        #endregion

        #region Events

        /// <summary>
        /// 插件库变化事件
        /// </summary>
        public event EventHandler<PluginLibraryChangedEventArgs>? PluginChanged;

        /// <summary>
        /// 触发插件变化事件
        /// </summary>
        protected virtual void OnPluginChanged(PluginLibraryChangedEventArgs e)
        {
            PluginChanged?.Invoke(this, e);
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// 检查插件是否有可用更新
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>更新检查结果</returns>
        public UpdateCheckResult CheckForUpdate(string pluginId)
        {
            // 获取已安装版本
            var installedInfo = GetInstalledPluginInfo(pluginId);
            if (installedInfo == null)
            {
                return UpdateCheckResult.NoUpdate(pluginId, "未安装");
            }

            var currentVersion = installedInfo.Version ?? "1.0.0";

            // 检查内置插件目录是否有该插件
            var builtInPath = Path.Combine(AppPaths.BuiltInPluginsDirectory, pluginId);
            if (!Directory.Exists(builtInPath))
            {
                return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
            }

            // 读取内置插件的清单
            var manifestPath = Path.Combine(builtInPath, "plugin.json");
            var manifestResult = PluginManifest.LoadFromFile(manifestPath);
            if (!manifestResult.IsSuccess || manifestResult.Manifest == null)
            {
                return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
            }

            var availableVersion = manifestResult.Manifest.Version ?? "1.0.0";

            // 比较版本号
            if (IsNewerVersion(currentVersion, availableVersion))
            {
                return UpdateCheckResult.WithUpdate(pluginId, currentVersion, availableVersion, builtInPath);
            }

            return UpdateCheckResult.NoUpdate(pluginId, currentVersion);
        }

        /// <summary>
        /// 检查所有已安装插件的更新
        /// </summary>
        /// <returns>有更新的插件列表</returns>
        public List<UpdateCheckResult> CheckAllUpdates()
        {
            var results = new List<UpdateCheckResult>();
            var installedPlugins = GetInstalledPlugins();

            foreach (var plugin in installedPlugins)
            {
                var checkResult = CheckForUpdate(plugin.Id);
                if (checkResult.HasUpdate)
                {
                    results.Add(checkResult);
                }
            }

            return results;
        }

        /// <summary>
        /// 更新插件到最新版本
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>更新结果</returns>
        public UpdateResult UpdatePlugin(string pluginId)
        {
            // 检查是否有可用更新
            var checkResult = CheckForUpdate(pluginId);
            if (!checkResult.HasUpdate)
            {
                return UpdateResult.NoUpdateAvailable();
            }

            var oldVersion = checkResult.CurrentVersion;
            var newVersion = checkResult.AvailableVersion!;
            var sourcePath = checkResult.SourcePath!;

            // 获取目标目录
            var targetDir = GetPluginDirectory(pluginId);

            try
            {
                // 删除旧文件（保留配置目录 - 配置在 Profile 目录中，不在这里）
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                // 复制新文件
                CopyDirectory(sourcePath, targetDir);

                // 更新索引中的版本号
                lock (_indexLock)
                {
                    var entry = _index.Plugins.FirstOrDefault(p =>
                        string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

                    if (entry != null)
                    {
                        entry.Version = newVersion;
                        SaveIndex();
                    }
                }

                // 获取更新后的插件信息
                var pluginInfo = GetInstalledPluginInfo(pluginId);

                // 触发 PluginChanged 事件 (Updated)
                OnPluginChanged(new PluginLibraryChangedEventArgs(
                    PluginLibraryChangeType.Updated, pluginId, pluginInfo));

                return UpdateResult.Success(oldVersion, newVersion);
            }
            catch (Exception ex)
            {
                return UpdateResult.Failed($"更新失败: {ex.Message}");
            }
        }

        #endregion

        #region Version Comparison

        /// <summary>
        /// 比较两个语义化版本号
        /// </summary>
        /// <param name="version1">第一个版本号</param>
        /// <param name="version2">第二个版本号</param>
        /// <returns>
        /// 负数: version1 &lt; version2
        /// 零: version1 == version2
        /// 正数: version1 &gt; version2
        /// </returns>
        public static int CompareVersions(string? version1, string? version2)
        {
            // 处理空值情况
            if (string.IsNullOrEmpty(version1) && string.IsNullOrEmpty(version2))
                return 0;
            if (string.IsNullOrEmpty(version1))
                return -1;
            if (string.IsNullOrEmpty(version2))
                return 1;

            // 移除可能的 'v' 前缀
            version1 = version1.TrimStart('v', 'V');
            version2 = version2.TrimStart('v', 'V');

            // 分割版本号
            var parts1 = version1.Split('.', '-', '+');
            var parts2 = version2.Split('.', '-', '+');

            // 比较主要版本部分 (major.minor.patch)
            var maxParts = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxParts; i++)
            {
                var part1 = i < parts1.Length ? parts1[i] : "0";
                var part2 = i < parts2.Length ? parts2[i] : "0";

                // 尝试作为数字比较
                if (int.TryParse(part1, out int num1) && int.TryParse(part2, out int num2))
                {
                    if (num1 != num2)
                        return num1.CompareTo(num2);
                }
                else
                {
                    // 作为字符串比较（用于预发布标签等）
                    var strCompare = string.Compare(part1, part2, StringComparison.OrdinalIgnoreCase);
                    if (strCompare != 0)
                        return strCompare;
                }
            }

            return 0;
        }

        /// <summary>
        /// 检查 availableVersion 是否比 currentVersion 更新
        /// </summary>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="availableVersion">可用版本</param>
        /// <returns>如果可用版本更新则返回 true</returns>
        public static bool IsNewerVersion(string? currentVersion, string? availableVersion)
        {
            return CompareVersions(availableVersion, currentVersion) > 0;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 获取可用的内置插件（未安装的）
        /// </summary>
        public List<PluginManifest> GetAvailableBuiltInPlugins()
        {
            var result = new List<PluginManifest>();

            if (!Directory.Exists(AppPaths.BuiltInPluginsDirectory))
                return result;

            foreach (var dir in Directory.GetDirectories(AppPaths.BuiltInPluginsDirectory))
            {
                var pluginId = Path.GetFileName(dir);
                
                // 跳过已安装的
                if (IsInstalled(pluginId))
                    continue;

                var manifestPath = Path.Combine(dir, "plugin.json");
                var manifestResult = PluginManifest.LoadFromFile(manifestPath);
                if (manifestResult.IsSuccess && manifestResult.Manifest != null)
                {
                    result.Add(manifestResult.Manifest);
                }
            }

            return result;
        }

        #endregion
    }
}
