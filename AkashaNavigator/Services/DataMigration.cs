using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
#region Result Types

/// <summary>
/// 迁移结果状态
/// </summary>
public enum MigrationResultStatus
{
    /// <summary>迁移成功</summary>
    Success,
    /// <summary>无需迁移</summary>
    NotNeeded,
    /// <summary>迁移失败</summary>
    Failed,
    /// <summary>部分成功</summary>
    PartialSuccess
}

/// <summary>
/// 迁移结果
/// </summary>
public class MigrationResult
{
    /// <summary>迁移状态</summary>
    public MigrationResultStatus Status { get; private set; }

    /// <summary>错误消息</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>迁移的插件数量</summary>
    public int MigratedPluginCount { get; private set; }

    /// <summary>迁移的 Profile 数量</summary>
    public int MigratedProfileCount { get; private set; }

    /// <summary>警告列表</summary>
    public List<string> Warnings { get; private set; } = new();

    private MigrationResult()
    {
    }

    public bool IsSuccess => Status == MigrationResultStatus.Success || Status == MigrationResultStatus.NotNeeded;

    public static MigrationResult Success(int pluginCount, int profileCount) => new() {
        Status = MigrationResultStatus.Success, MigratedPluginCount = pluginCount, MigratedProfileCount = profileCount
    };

    public static MigrationResult NotNeeded() => new() { Status = MigrationResultStatus.NotNeeded };

    public static MigrationResult Failed(string message) => new() { Status = MigrationResultStatus.Failed,
                                                                    ErrorMessage = message };

    public static MigrationResult PartialSuccess(int pluginCount, int profileCount, List<string> warnings) => new() {
        Status = MigrationResultStatus.PartialSuccess, MigratedPluginCount = pluginCount,
        MigratedProfileCount = profileCount, Warnings = warnings
    };
}

/// <summary>
/// 迁移备份信息
/// </summary>
public class MigrationBackup
{
    public string BackupDirectory { get; set; } = string.Empty;
    public List<string> BackedUpProfiles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

#endregion

/// <summary>
/// 数据迁移服务
/// 负责将旧版本数据结构迁移到新架构
/// 旧结构: Profile目录中内嵌插件 (User/Data/Profiles/{profileId}/plugins/{pluginId}/)
/// 新结构: 全局插件库 + Profile引用清单
/// </summary>
public class DataMigration
{
#region Singleton

    private static DataMigration? _instance;

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static DataMigration Instance
    {
        get
        {
            if (_instance == null)
            {
                // 使用 LogService.Instance 获取日志服务（插件系统专用）
                _instance = new DataMigration(LogService.Instance);
            }
            return _instance;
        }
    }

#endregion

#region Properties

    /// <summary>
    /// Profiles 目录
    /// </summary>
    public string ProfilesDirectory => AppPaths.ProfilesDirectory;

    /// <summary>
    /// 全局插件库目录
    /// </summary>
    public string InstalledPluginsDirectory => AppPaths.InstalledPluginsDirectory;

    /// <summary>
    /// 插件库索引文件路径
    /// </summary>
    public string LibraryIndexPath => AppPaths.LibraryIndexPath;

    /// <summary>
    /// 关联索引文件路径
    /// </summary>
    public string AssociationsFilePath => AppPaths.AssociationsFilePath;

    /// <summary>
    /// 备份目录
    /// </summary>
    public string BackupDirectory => Path.Combine(AppPaths.DataDirectory, "migration-backup");

#endregion

#region Fields

    private readonly ILogService _logService;

#endregion

#region Constructor

    /// <summary>
    /// DI容器使用的构造函数
    /// </summary>
    public DataMigration(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

#endregion

#region Detection Methods

    /// <summary>
    /// 检测是否需要迁移
    /// 条件：存在旧版本的 Profile 目录结构（插件内嵌在 Profile 目录中）
    /// </summary>
    /// <returns>是否需要迁移</returns>
    public bool NeedsMigration()
    {
        // 如果已存在新版本的索引文件，说明已经迁移过
        if (File.Exists(LibraryIndexPath) && File.Exists(AssociationsFilePath))
        {
            // 但仍需检查是否有未迁移的旧数据
            return HasOldStylePlugins();
        }

        // 检查是否存在旧版本的插件目录结构
        return HasOldStylePlugins();
    }

    /// <summary>
    /// 检查是否存在旧版本的插件目录结构
    /// 旧结构: User/Data/Profiles/{profileId}/plugins/{pluginId}/main.js
    /// </summary>
    private bool HasOldStylePlugins()
    {
        if (!Directory.Exists(ProfilesDirectory))
            return false;

        foreach (var profileDir in Directory.GetDirectories(ProfilesDirectory))
        {
            var pluginsDir = Path.Combine(profileDir, "plugins");
            if (Directory.Exists(pluginsDir))
            {
                // 检查是否有有效的插件目录
                foreach (var pluginDir in Directory.GetDirectories(pluginsDir))
                {
                    var manifestPath = Path.Combine(pluginDir, "plugin.json");
                    var mainJsPath = Path.Combine(pluginDir, "main.js");

                    if (File.Exists(manifestPath) || File.Exists(mainJsPath))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取需要迁移的插件信息
    /// </summary>
    /// <returns>Profile ID -> 插件目录列表 的映射</returns>
    public Dictionary<string, List<OldPluginInfo>> GetOldStylePlugins()
    {
        var result = new Dictionary<string, List<OldPluginInfo>>();

        if (!Directory.Exists(ProfilesDirectory))
            return result;

        foreach (var profileDir in Directory.GetDirectories(ProfilesDirectory))
        {
            var profileId = Path.GetFileName(profileDir);
            var pluginsDir = Path.Combine(profileDir, "plugins");

            if (!Directory.Exists(pluginsDir))
                continue;

            var plugins = new List<OldPluginInfo>();

            foreach (var pluginDir in Directory.GetDirectories(pluginsDir))
            {
                var pluginDirName = Path.GetFileName(pluginDir);
                var manifestPath = Path.Combine(pluginDir, "plugin.json");

                if (!File.Exists(manifestPath))
                    continue;

                var manifestResult = PluginManifest.LoadFromFile(manifestPath);
                if (!manifestResult.IsSuccess || manifestResult.Manifest == null)
                    continue;

                plugins.Add(new OldPluginInfo { PluginId = manifestResult.Manifest.Id ?? pluginDirName,
                                                PluginDirectory = pluginDir, Manifest = manifestResult.Manifest,
                                                ProfileId = profileId });
            }

            if (plugins.Count > 0)
            {
                result[profileId] = plugins;
            }
        }

        return result;
    }

#endregion

#region Migration Methods

    /// <summary>
    /// 执行数据迁移
    /// </summary>
    /// <returns>迁移结果</returns>
    public MigrationResult Migrate()
    {
        if (!NeedsMigration())
        {
            return MigrationResult.NotNeeded();
        }

        MigrationBackup? backup = null;
        var warnings = new List<string>();
        int migratedPluginCount = 0;
        int migratedProfileCount = 0;

        try
        {
            // 1. 创建备份
            backup = CreateBackup();
            _logService.Info("DataMigration", "已创建迁移备份: {BackupDirectory}", backup.BackupDirectory);

            // 2. 获取需要迁移的插件
            var oldPlugins = GetOldStylePlugins();
            if (oldPlugins.Count == 0)
            {
                return MigrationResult.NotNeeded();
            }

            // 3. 收集所有唯一的插件（去重）
            var uniquePlugins = new Dictionary<string, OldPluginInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in oldPlugins)
            {
                foreach (var plugin in kvp.Value)
                {
                    if (!uniquePlugins.ContainsKey(plugin.PluginId))
                    {
                        uniquePlugins[plugin.PluginId] = plugin;
                    }
                }
            }

            // 4. 将插件本体移动到全局插件库
            var libraryIndex = new PluginLibraryIndex();
            if (File.Exists(LibraryIndexPath))
            {
                libraryIndex = PluginLibraryIndex.LoadFromFile(LibraryIndexPath);
            }

            foreach (var kvp in uniquePlugins)
            {
                var pluginId = kvp.Key;
                var pluginInfo = kvp.Value;

                // 检查是否已在全局库中
                if (libraryIndex.Plugins.Any(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logService.Debug("DataMigration", "插件 {PluginId} 已在全局库中，跳过", pluginId);
                    continue;
                }

                try
                {
                    // 复制插件到全局库
                    var targetDir = Path.Combine(InstalledPluginsDirectory, pluginId);
                    CopyDirectory(pluginInfo.PluginDirectory, targetDir);

                    // 添加到索引
                    libraryIndex.Plugins.Add(
                        new InstalledPluginEntry { Id = pluginId, Version = pluginInfo.Manifest?.Version ?? "1.0.0",
                                                   InstalledAt = DateTime.Now, Source = "migrated" });

                    migratedPluginCount++;
                    _logService.Info("DataMigration", "已迁移插件: {PluginId}", pluginId);
                }
                catch (Exception ex)
                {
                    warnings.Add($"迁移插件 {pluginId} 失败: {ex.Message}");
                    _logService.Warn("DataMigration", "迁移插件 {PluginId} 失败: {ErrorMessage}", pluginId,
                                             ex.Message);
                }
            }

            // 保存插件库索引
            libraryIndex.SaveToFile(LibraryIndexPath);

            // 5. 创建关联索引
            var associationIndex = new AssociationIndex();
            if (File.Exists(AssociationsFilePath))
            {
                associationIndex = AssociationIndex.LoadFromFile(AssociationsFilePath);
            }

            foreach (var kvp in oldPlugins)
            {
                var profileId = kvp.Key;
                var plugins = kvp.Value;

                if (!associationIndex.ProfilePlugins.ContainsKey(profileId))
                {
                    associationIndex.ProfilePlugins[profileId] = new List<PluginReferenceEntry>();
                }

                var entries = associationIndex.ProfilePlugins[profileId];

                foreach (var plugin in plugins)
                {
                    // 检查是否已存在关联
                    if (entries.Any(e =>
                                        string.Equals(e.PluginId, plugin.PluginId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    entries.Add(new PluginReferenceEntry { PluginId = plugin.PluginId, Enabled = true,
                                                           AddedAt = DateTime.Now });
                }

                migratedProfileCount++;
            }

            // 保存关联索引
            associationIndex.SaveToFile(AssociationsFilePath);

            // 6. 删除旧的插件目录
            foreach (var kvp in oldPlugins)
            {
                var profileId = kvp.Key;
                var profileDir = Path.Combine(ProfilesDirectory, profileId);
                var pluginsDir = Path.Combine(profileDir, "plugins");

                try
                {
                    if (Directory.Exists(pluginsDir))
                    {
                        Directory.Delete(pluginsDir, true);
                        _logService.Debug("DataMigration", "已删除旧插件目录: {PluginsDir}", pluginsDir);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"删除旧插件目录失败 [{profileId}]: {ex.Message}");
                    _logService.Warn("DataMigration", "删除旧插件目录失败 [{PluginsDir}]: {ErrorMessage}",
                                             pluginsDir, ex.Message);
                }
            }

            // 7. 验证迁移结果
            var validationErrors = ValidateMigration(uniquePlugins.Keys.ToList());
            if (validationErrors.Count > 0)
            {
                warnings.AddRange(validationErrors);
            }

            _logService.Info("DataMigration",
                                     $"迁移完成: {migratedPluginCount} 个插件, {migratedProfileCount} 个 Profile");

            if (warnings.Count > 0)
            {
                return MigrationResult.PartialSuccess(migratedPluginCount, migratedProfileCount, warnings);
            }

            return MigrationResult.Success(migratedPluginCount, migratedProfileCount);
        }
        catch (Exception ex)
        {
            _logService.Error("DataMigration", ex, "迁移失败");

            // 尝试回滚
            if (backup != null)
            {
                try
                {
                    Rollback(backup);
                    _logService.Info("DataMigration", "已回滚迁移更改");
                }
                catch (Exception rollbackEx)
                {
                    _logService.Error("DataMigration", rollbackEx, "回滚失败");
                }
            }

            return MigrationResult.Failed(ex.Message);
        }
    }

#endregion

#region Backup and Rollback

    /// <summary>
    /// 创建迁移备份
    /// </summary>
    private MigrationBackup CreateBackup()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(BackupDirectory, timestamp);
        Directory.CreateDirectory(backupDir);

        var backup = new MigrationBackup { BackupDirectory = backupDir, CreatedAt = DateTime.Now };

        // 备份 Profiles 目录中的插件
        if (Directory.Exists(ProfilesDirectory))
        {
            foreach (var profileDir in Directory.GetDirectories(ProfilesDirectory))
            {
                var profileId = Path.GetFileName(profileDir);
                var pluginsDir = Path.Combine(profileDir, "plugins");

                if (Directory.Exists(pluginsDir))
                {
                    var backupPluginsDir = Path.Combine(backupDir, "Profiles", profileId, "plugins");
                    CopyDirectory(pluginsDir, backupPluginsDir);
                    backup.BackedUpProfiles.Add(profileId);
                }
            }
        }

        // 备份现有的索引文件
        if (File.Exists(LibraryIndexPath))
        {
            File.Copy(LibraryIndexPath, Path.Combine(backupDir, "library.json"), true);
        }

        if (File.Exists(AssociationsFilePath))
        {
            File.Copy(AssociationsFilePath, Path.Combine(backupDir, "associations.json"), true);
        }

        return backup;
    }

    /// <summary>
    /// 回滚迁移更改
    /// </summary>
    /// <param name="backup">备份信息</param>
    public void Rollback(MigrationBackup backup)
    {
        if (backup == null || string.IsNullOrEmpty(backup.BackupDirectory))
        {
            throw new ArgumentException("无效的备份信息");
        }

        if (!Directory.Exists(backup.BackupDirectory))
        {
            throw new DirectoryNotFoundException($"备份目录不存在: {backup.BackupDirectory}");
        }

        // 恢复 Profiles 目录中的插件
        var backupProfilesDir = Path.Combine(backup.BackupDirectory, "Profiles");
        if (Directory.Exists(backupProfilesDir))
        {
            foreach (var profileId in backup.BackedUpProfiles)
            {
                var backupPluginsDir = Path.Combine(backupProfilesDir, profileId, "plugins");
                if (Directory.Exists(backupPluginsDir))
                {
                    var targetPluginsDir = Path.Combine(ProfilesDirectory, profileId, "plugins");

                    // 删除可能已创建的新目录
                    if (Directory.Exists(targetPluginsDir))
                    {
                        Directory.Delete(targetPluginsDir, true);
                    }

                    // 恢复备份
                    CopyDirectory(backupPluginsDir, targetPluginsDir);
                }
            }
        }

        // 恢复索引文件
        var backupLibraryIndex = Path.Combine(backup.BackupDirectory, "library.json");
        if (File.Exists(backupLibraryIndex))
        {
            File.Copy(backupLibraryIndex, LibraryIndexPath, true);
        }
        else if (File.Exists(LibraryIndexPath))
        {
            // 如果备份中没有索引文件，说明迁移前不存在，删除新创建的
            File.Delete(LibraryIndexPath);
        }

        var backupAssociationsIndex = Path.Combine(backup.BackupDirectory, "associations.json");
        if (File.Exists(backupAssociationsIndex))
        {
            File.Copy(backupAssociationsIndex, AssociationsFilePath, true);
        }
        else if (File.Exists(AssociationsFilePath))
        {
            File.Delete(AssociationsFilePath);
        }

        // 删除迁移过程中创建的全局插件库中的插件
        // 注意：这里只删除迁移过程中添加的，不删除之前就存在的
        // 由于我们无法精确知道哪些是新添加的，这里选择保守策略，不删除

        _logService.Info("DataMigration", "迁移回滚完成");
    }

#endregion

#region Validation

    /// <summary>
    /// 验证迁移结果
    /// </summary>
    /// <param name="expectedPluginIds">期望迁移的插件ID列表</param>
    /// <returns>验证错误列表</returns>
    private List<string> ValidateMigration(List<string> expectedPluginIds)
    {
        var errors = new List<string>();

        // 检查插件库索引
        if (!File.Exists(LibraryIndexPath))
        {
            errors.Add("插件库索引文件不存在");
            return errors;
        }

        var libraryIndex = PluginLibraryIndex.LoadFromFile(LibraryIndexPath);

        // 检查每个期望的插件是否在索引中
        foreach (var pluginId in expectedPluginIds)
        {
            if (!libraryIndex.Plugins.Any(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"插件 {pluginId} 未在索引中");
                continue;
            }

            // 检查插件目录是否存在
            var pluginDir = Path.Combine(InstalledPluginsDirectory, pluginId);
            if (!Directory.Exists(pluginDir))
            {
                errors.Add($"插件目录不存在: {pluginDir}");
                continue;
            }

            // 检查必要文件
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                errors.Add($"插件清单文件不存在: {manifestPath}");
            }
        }

        // 检查关联索引
        if (!File.Exists(AssociationsFilePath))
        {
            errors.Add("关联索引文件不存在");
        }

        return errors;
    }

#endregion

#region Utility Methods

    /// <summary>
    /// 复制目录及其内容
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(dir, targetSubDir);
        }
    }

#endregion
}

#region Helper Classes

/// <summary>
/// 旧版本插件信息
/// </summary>
public class OldPluginInfo
{
    /// <summary>插件ID</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>插件目录路径</summary>
    public string PluginDirectory { get; set; } = string.Empty;

    /// <summary>插件清单</summary>
    public PluginManifest? Manifest { get; set; }

    /// <summary>所属 Profile ID</summary>
    public string ProfileId { get; set; } = string.Empty;
}

#endregion
}
