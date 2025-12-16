using System;
using System.Collections.Generic;
using System.Linq;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    #region Event Args

    /// <summary>
    /// 关联变化类型
    /// </summary>
    public enum AssociationChangeType
    {
        /// <summary>添加插件到Profile</summary>
        Added,
        /// <summary>从Profile移除插件</summary>
        Removed,
        /// <summary>批量添加</summary>
        BatchAdded,
        /// <summary>批量移除</summary>
        BatchRemoved
    }

    /// <summary>
    /// 关联变化事件参数
    /// </summary>
    public class AssociationChangedEventArgs : EventArgs
    {
        /// <summary>变化类型</summary>
        public AssociationChangeType ChangeType { get; }

        /// <summary>涉及的插件ID</summary>
        public string PluginId { get; }

        /// <summary>涉及的Profile ID</summary>
        public string ProfileId { get; }

        /// <summary>批量操作时涉及的多个插件ID</summary>
        public List<string>? PluginIds { get; }

        /// <summary>批量操作时涉及的多个Profile ID</summary>
        public List<string>? ProfileIds { get; }

        public AssociationChangedEventArgs(AssociationChangeType changeType, string pluginId, string profileId)
        {
            ChangeType = changeType;
            PluginId = pluginId;
            ProfileId = profileId;
        }

        public AssociationChangedEventArgs(AssociationChangeType changeType, List<string> pluginIds, string profileId)
        {
            ChangeType = changeType;
            PluginId = string.Empty;
            ProfileId = profileId;
            PluginIds = pluginIds;
        }

        public AssociationChangedEventArgs(AssociationChangeType changeType, string pluginId, List<string> profileIds)
        {
            ChangeType = changeType;
            PluginId = pluginId;
            ProfileId = string.Empty;
            ProfileIds = profileIds;
        }
    }

    #endregion


    /// <summary>
    /// 插件关联管理服务
    /// 管理插件与Profile的关联关系，支持双向查询
    /// </summary>
    public class PluginAssociationManager
    {
        #region Singleton

        private static PluginAssociationManager? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static PluginAssociationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PluginAssociationManager();
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
        /// 关联索引文件路径
        /// </summary>
        public string AssociationsFilePath => AppPaths.AssociationsFilePath;

        #endregion

        #region Fields

        private AssociationIndex _index;
        private readonly object _indexLock = new();

        #endregion

        #region Constructor

        private PluginAssociationManager()
        {
            _index = LoadIndex();
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        internal PluginAssociationManager(string indexPath)
        {
            _index = AssociationIndex.LoadFromFile(indexPath);
        }

        #endregion

        #region Index Management

        /// <summary>
        /// 加载索引文件
        /// </summary>
        private AssociationIndex LoadIndex()
        {
            return AssociationIndex.LoadFromFile(AssociationsFilePath);
        }

        /// <summary>
        /// 保存索引文件
        /// </summary>
        private void SaveIndex()
        {
            lock (_indexLock)
            {
                _index.SaveToFile(AssociationsFilePath);
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


        #region Plugin -> Profile Query (插件到Profile查询)

        /// <summary>
        /// 获取使用指定插件的所有Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>使用该插件的Profile ID列表</returns>
        public List<string> GetProfilesUsingPlugin(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
                return new List<string>();

            lock (_indexLock)
            {
                var result = new List<string>();

                foreach (var kvp in _index.ProfilePlugins)
                {
                    var profileId = kvp.Key;
                    var plugins = kvp.Value;

                    if (plugins.Any(p => string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(profileId);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 获取插件被引用的次数
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>引用次数</returns>
        public int GetPluginReferenceCount(string pluginId)
        {
            return GetProfilesUsingPlugin(pluginId).Count;
        }

        #endregion

        #region Profile -> Plugin Query (Profile到插件查询)

        /// <summary>
        /// 获取Profile引用的所有插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件引用列表</returns>
        public List<PluginReference> GetPluginsInProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return new List<PluginReference>();

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return new List<PluginReference>();

                var result = new List<PluginReference>();
                foreach (var entry in entries)
                {
                    var reference = entry.ToReference();
                    
                    // 计算安装状态
                    if (!reference.Enabled)
                    {
                        reference.Status = PluginInstallStatus.Disabled;
                    }
                    else if (!PluginLibrary.Instance.IsInstalled(reference.PluginId))
                    {
                        reference.Status = PluginInstallStatus.Missing;
                    }
                    else
                    {
                        reference.Status = PluginInstallStatus.Installed;
                    }

                    result.Add(reference);
                }

                return result;
            }
        }

        /// <summary>
        /// 获取Profile中缺失的插件（引用但未安装）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>缺失的插件ID列表</returns>
        public List<string> GetMissingPlugins(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return new List<string>();

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return new List<string>();

                var result = new List<string>();
                foreach (var entry in entries)
                {
                    if (!PluginLibrary.Instance.IsInstalled(entry.PluginId))
                    {
                        result.Add(entry.PluginId);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// 检查Profile是否包含指定插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否包含</returns>
        public bool ProfileContainsPlugin(string profileId, string pluginId)
        {
            if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(pluginId))
                return false;

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return false;

                return entries.Any(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            }
        }

        #endregion


        #region Association Operations (关联操作)

        /// <summary>
        /// 添加插件到Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <param name="enabled">是否启用（默认true）</param>
        /// <returns>是否成功添加（如果已存在则返回false）</returns>
        public bool AddPluginToProfile(string pluginId, string profileId, bool enabled = true)
        {
            if (string.IsNullOrEmpty(pluginId) || string.IsNullOrEmpty(profileId))
                return false;

            lock (_indexLock)
            {
                // 确保Profile条目存在
                if (!_index.ProfilePlugins.ContainsKey(profileId))
                {
                    _index.ProfilePlugins[profileId] = new List<PluginReferenceEntry>();
                }

                var entries = _index.ProfilePlugins[profileId];

                // 检查是否已存在
                if (entries.Any(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                // 添加新引用
                entries.Add(new PluginReferenceEntry
                {
                    PluginId = pluginId,
                    Enabled = enabled,
                    AddedAt = DateTime.Now
                });

                SaveIndex();
            }

            // 触发事件
            OnAssociationChanged(new AssociationChangedEventArgs(
                AssociationChangeType.Added, pluginId, profileId));

            return true;
        }

        /// <summary>
        /// 从Profile移除插件引用
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功移除（如果不存在则返回false）</returns>
        public bool RemovePluginFromProfile(string pluginId, string profileId)
        {
            if (string.IsNullOrEmpty(pluginId) || string.IsNullOrEmpty(profileId))
                return false;

            bool removed = false;

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return false;

                var countBefore = entries.Count;
                entries.RemoveAll(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
                removed = entries.Count < countBefore;

                if (removed)
                {
                    // 如果Profile没有任何插件了，可以选择保留空列表或删除条目
                    // 这里选择保留空列表，以便保持Profile的存在记录
                    SaveIndex();
                }
            }

            if (removed)
            {
                // 触发事件
                OnAssociationChanged(new AssociationChangedEventArgs(
                    AssociationChangeType.Removed, pluginId, profileId));
            }

            return removed;
        }

        /// <summary>
        /// 批量添加插件到Profile
        /// </summary>
        /// <param name="pluginIds">插件ID列表</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>成功添加的数量</returns>
        public int AddPluginsToProfile(IEnumerable<string> pluginIds, string profileId)
        {
            if (pluginIds == null || string.IsNullOrEmpty(profileId))
                return 0;

            var pluginIdList = pluginIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (pluginIdList.Count == 0)
                return 0;

            int addedCount = 0;
            var addedPlugins = new List<string>();

            lock (_indexLock)
            {
                // 确保Profile条目存在
                if (!_index.ProfilePlugins.ContainsKey(profileId))
                {
                    _index.ProfilePlugins[profileId] = new List<PluginReferenceEntry>();
                }

                var entries = _index.ProfilePlugins[profileId];

                foreach (var pluginId in pluginIdList)
                {
                    // 检查是否已存在
                    if (entries.Any(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // 添加新引用
                    entries.Add(new PluginReferenceEntry
                    {
                        PluginId = pluginId,
                        Enabled = true,
                        AddedAt = DateTime.Now
                    });

                    addedPlugins.Add(pluginId);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    SaveIndex();
                }
            }

            if (addedCount > 0)
            {
                // 触发事件
                OnAssociationChanged(new AssociationChangedEventArgs(
                    AssociationChangeType.BatchAdded, addedPlugins, profileId));
            }

            return addedCount;
        }

        /// <summary>
        /// 批量添加插件到多个Profile
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="profileIds">Profile ID列表</param>
        /// <returns>成功添加的数量</returns>
        public int AddPluginToProfiles(string pluginId, IEnumerable<string> profileIds)
        {
            if (string.IsNullOrEmpty(pluginId) || profileIds == null)
                return 0;

            var profileIdList = profileIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (profileIdList.Count == 0)
                return 0;

            int addedCount = 0;
            var addedProfiles = new List<string>();

            lock (_indexLock)
            {
                foreach (var profileId in profileIdList)
                {
                    // 确保Profile条目存在
                    if (!_index.ProfilePlugins.ContainsKey(profileId))
                    {
                        _index.ProfilePlugins[profileId] = new List<PluginReferenceEntry>();
                    }

                    var entries = _index.ProfilePlugins[profileId];

                    // 检查是否已存在
                    if (entries.Any(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // 添加新引用
                    entries.Add(new PluginReferenceEntry
                    {
                        PluginId = pluginId,
                        Enabled = true,
                        AddedAt = DateTime.Now
                    });

                    addedProfiles.Add(profileId);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    SaveIndex();
                }
            }

            if (addedCount > 0)
            {
                // 触发事件
                OnAssociationChanged(new AssociationChangedEventArgs(
                    AssociationChangeType.BatchAdded, pluginId, addedProfiles));
            }

            return addedCount;
        }

        /// <summary>
        /// 从所有Profile中移除指定插件的引用
        /// （用于插件卸载时清理关联）
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>被移除引用的Profile数量</returns>
        public int RemovePluginFromAllProfiles(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId))
                return 0;

            int removedCount = 0;
            var affectedProfiles = new List<string>();

            lock (_indexLock)
            {
                foreach (var kvp in _index.ProfilePlugins)
                {
                    var profileId = kvp.Key;
                    var entries = kvp.Value;

                    var countBefore = entries.Count;
                    entries.RemoveAll(e => string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

                    if (entries.Count < countBefore)
                    {
                        affectedProfiles.Add(profileId);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    SaveIndex();
                }
            }

            if (removedCount > 0)
            {
                // 触发事件
                OnAssociationChanged(new AssociationChangedEventArgs(
                    AssociationChangeType.BatchRemoved, pluginId, affectedProfiles));
            }

            return removedCount;
        }

        #endregion


        #region Plugin Enable/Disable

        /// <summary>
        /// 设置插件在Profile中的启用状态
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件ID</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>是否成功设置</returns>
        public bool SetPluginEnabled(string profileId, string pluginId, bool enabled)
        {
            if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(pluginId))
                return false;

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return false;

                var entry = entries.FirstOrDefault(e => 
                    string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                    return false;

                entry.Enabled = enabled;
                SaveIndex();
            }

            return true;
        }

        /// <summary>
        /// 获取插件在Profile中的启用状态
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否启用（如果不存在则返回null）</returns>
        public bool? GetPluginEnabled(string profileId, string pluginId)
        {
            if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(pluginId))
                return null;

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.TryGetValue(profileId, out var entries))
                    return null;

                var entry = entries.FirstOrDefault(e => 
                    string.Equals(e.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));

                return entry?.Enabled;
            }
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// 删除Profile的所有关联（用于Profile删除时）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功删除</returns>
        public bool RemoveProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return false;

            lock (_indexLock)
            {
                if (!_index.ProfilePlugins.ContainsKey(profileId))
                    return false;

                _index.ProfilePlugins.Remove(profileId);
                SaveIndex();
            }

            return true;
        }

        /// <summary>
        /// 获取所有有关联的Profile ID列表
        /// </summary>
        /// <returns>Profile ID列表</returns>
        public List<string> GetAllProfileIds()
        {
            lock (_indexLock)
            {
                return _index.ProfilePlugins.Keys.ToList();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 关联变化事件
        /// </summary>
        public event EventHandler<AssociationChangedEventArgs>? AssociationChanged;

        /// <summary>
        /// 触发关联变化事件
        /// </summary>
        protected virtual void OnAssociationChanged(AssociationChangedEventArgs e)
        {
            AssociationChanged?.Invoke(this, e);
        }

        #endregion
    }
}
