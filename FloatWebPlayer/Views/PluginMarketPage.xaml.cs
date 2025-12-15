using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件市场页面视图模型 - Profile 选择项
    /// </summary>
    public class ProfileSelectItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 权限标签视图模型
    /// </summary>
    public class PermissionTagItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsWarning { get; set; }
        public Style? TagStyle { get; set; }
        public Brush TextColor => IsWarning ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) : new SolidColorBrush(Color.FromRgb(170, 170, 170));
    }

    /// <summary>
    /// 插件市场页面视图模型 - 插件项
    /// </summary>
    public class PluginMarketItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public List<PermissionTagItem> PermissionTags { get; set; } = new();
        public bool IsSubscribed { get; set; }
        public bool IsEnabled { get; set; }
        public bool HasPermissions => Permissions.Count > 0;
    }

    /// <summary>
    /// 插件管理页面
    /// 显示已订阅和可用的插件，支持订阅/取消订阅/启用/禁用
    /// </summary>
    public partial class PluginMarketPage : UserControl
    {
        /// <summary>
        /// 敏感权限列表（需要警告标识）
        /// </summary>
        private static readonly HashSet<string> SensitivePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            "network", "http", "storage", "config"
        };

        /// <summary>
        /// 权限说明映射
        /// </summary>
        private static readonly Dictionary<string, string> PermissionDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "subtitle", "读取视频字幕" },
            { "overlay", "显示覆盖层" },
            { "network", "访问网络" },
            { "http", "发送 HTTP 请求" },
            { "storage", "读写本地存储" },
            { "config", "读写插件配置" },
            { "player", "控制播放器" },
            { "window", "控制窗口" },
            { "events", "监听系统事件" },
            { "speech", "语音合成" },
            { "core", "核心功能" }
        };

        /// <summary>
        /// 当前选中的 Profile ID
        /// </summary>
        private string _currentProfileId = string.Empty;

        /// <summary>
        /// 插件订阅状态变化事件
        /// </summary>
        public event EventHandler? SubscriptionChanged;

        public PluginMarketPage()
        {
            InitializeComponent();
            
            // 加载 Profile 列表
            LoadProfileList();
        }

        /// <summary>
        /// 加载 Profile 下拉列表
        /// </summary>
        private void LoadProfileList()
        {
            var subscribedProfiles = SubscriptionManager.Instance.GetSubscribedProfiles();
            var items = new List<ProfileSelectItem>();

            foreach (var profileId in subscribedProfiles)
            {
                var profileInfo = ProfileRegistry.Instance.GetProfile(profileId);
                items.Add(new ProfileSelectItem
                {
                    Id = profileId,
                    Name = profileInfo?.Name ?? profileId
                });
            }

            ProfileSelector.ItemsSource = items;

            // 选择第一个 Profile（如果有）
            if (items.Count > 0)
            {
                ProfileSelector.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Profile 选择变化
        /// </summary>
        private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileSelector.SelectedItem is ProfileSelectItem selected)
            {
                _currentProfileId = selected.Id;
                RefreshPluginList();
            }
        }

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        public void RefreshPluginList()
        {
            if (string.IsNullOrEmpty(_currentProfileId))
            {
                SubscribedPluginList.ItemsSource = null;
                AvailablePluginList.ItemsSource = null;
                NoSubscribedMessage.Visibility = Visibility.Visible;
                NoAvailableMessage.Visibility = Visibility.Visible;
                return;
            }

            // 获取已订阅的插件
            var subscribedPluginIds = SubscriptionManager.Instance.GetSubscribedPlugins(_currentProfileId);
            
            // 获取所有内置插件
            var allPlugins = PluginRegistry.Instance.GetAllPlugins();

            // 构建已订阅插件列表
            var subscribedItems = new List<PluginMarketItem>();
            foreach (var pluginId in subscribedPluginIds)
            {
                var pluginInfo = PluginRegistry.Instance.GetPlugin(pluginId);
                if (pluginInfo == null) continue;

                // 获取插件启用状态
                var loadedPlugin = PluginHost.Instance.GetPlugin(pluginId);
                var isEnabled = loadedPlugin?.IsEnabled ?? true;

                subscribedItems.Add(CreatePluginMarketItem(pluginInfo, true, isEnabled));
            }

            // 构建可用插件列表（排除已订阅的）
            var availableItems = new List<PluginMarketItem>();
            foreach (var pluginInfo in allPlugins)
            {
                if (subscribedPluginIds.Contains(pluginInfo.Id, StringComparer.OrdinalIgnoreCase))
                    continue;

                availableItems.Add(CreatePluginMarketItem(pluginInfo, false, false));
            }

            // 更新 UI
            SubscribedPluginList.ItemsSource = subscribedItems;
            AvailablePluginList.ItemsSource = availableItems;

            NoSubscribedMessage.Visibility = subscribedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NoAvailableMessage.Visibility = availableItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 创建插件市场项
        /// </summary>
        private PluginMarketItem CreatePluginMarketItem(BuiltInPluginInfo pluginInfo, bool isSubscribed, bool isEnabled)
        {
            var permissionTags = new List<PermissionTagItem>();
            foreach (var permission in pluginInfo.Permissions)
            {
                var isWarning = SensitivePermissions.Contains(permission);
                permissionTags.Add(new PermissionTagItem
                {
                    Name = permission,
                    IsWarning = isWarning,
                    TagStyle = isWarning 
                        ? (Style)Resources["WarningPermissionTagStyle"] 
                        : (Style)Resources["PermissionTagStyle"]
                });
            }

            return new PluginMarketItem
            {
                Id = pluginInfo.Id,
                Name = pluginInfo.Name,
                Version = pluginInfo.Version,
                Description = pluginInfo.Description,
                Permissions = pluginInfo.Permissions,
                PermissionTags = permissionTags,
                IsSubscribed = isSubscribed,
                IsEnabled = isEnabled
            };
        }

        /// <summary>
        /// 订阅按钮点击
        /// </summary>
        private void BtnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string pluginId)
                return;

            if (string.IsNullOrEmpty(_currentProfileId))
            {
                MessageBox.Show("请先选择一个 Profile", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取插件信息
            var pluginInfo = PluginRegistry.Instance.GetPlugin(pluginId);
            if (pluginInfo == null)
            {
                MessageBox.Show($"未找到插件: {pluginId}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 获取当前 Profile 信息
            var profileInfo = ProfileRegistry.Instance.GetProfile(_currentProfileId);
            var profileName = profileInfo?.Name ?? _currentProfileId;

            // 构建权限说明
            var permissionDetails = new List<string>();
            foreach (var permission in pluginInfo.Permissions)
            {
                var desc = PermissionDescriptions.TryGetValue(permission, out var d) ? d : permission;
                var warning = SensitivePermissions.Contains(permission) ? " ⚠️" : "";
                permissionDetails.Add($"  • {permission} - {desc}{warning}");
            }

            // 显示订阅确认对话框
            var message = $"确定要将「{pluginInfo.Name}」订阅到「{profileName}」Profile 吗？";
            
            if (permissionDetails.Count > 0)
            {
                message += $"\n\n所需权限：\n{string.Join("\n", permissionDetails)}";
            }

            var result = MessageBox.Show(
                message,
                "订阅插件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 执行订阅
            var success = SubscriptionManager.Instance.SubscribePlugin(pluginId, _currentProfileId);
            
            if (success)
            {
                Debug.WriteLine($"[PluginMarket] 成功订阅插件: {pluginId} -> {_currentProfileId}");
                
                // 如果当前 Profile 正在运行，重新加载插件
                if (PluginHost.Instance.CurrentProfileId == _currentProfileId)
                {
                    PluginHost.Instance.LoadPluginsForProfile(_currentProfileId);
                }
                
                RefreshPluginList();
                SubscriptionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    $"订阅插件「{pluginInfo.Name}」失败",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消订阅按钮点击
        /// </summary>
        private void BtnUnsubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string pluginId)
                return;

            if (string.IsNullOrEmpty(_currentProfileId))
                return;

            // 获取插件信息
            var pluginInfo = PluginRegistry.Instance.GetPlugin(pluginId);
            if (pluginInfo == null)
            {
                MessageBox.Show($"未找到插件: {pluginId}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 显示确认对话框
            var result = MessageBox.Show(
                $"确定要取消订阅插件「{pluginInfo.Name}」吗？\n\n此操作将删除该插件的用户配置，无法撤销。",
                "确认取消订阅",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // 执行取消订阅
            var success = SubscriptionManager.Instance.UnsubscribePlugin(pluginId, _currentProfileId);

            if (success)
            {
                Debug.WriteLine($"[PluginMarket] 成功取消订阅插件: {pluginId} <- {_currentProfileId}");
                
                // 如果当前 Profile 正在运行，重新加载插件
                if (PluginHost.Instance.CurrentProfileId == _currentProfileId)
                {
                    PluginHost.Instance.LoadPluginsForProfile(_currentProfileId);
                }
                
                RefreshPluginList();
                SubscriptionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    $"取消订阅插件「{pluginInfo.Name}」失败",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 插件启用/禁用切换
        /// </summary>
        private void PluginToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.Tag is not string pluginId)
                return;

            var isEnabled = checkBox.IsChecked ?? false;
            
            // 更新 PluginHost 中的状态
            PluginHost.Instance.SetPluginEnabled(pluginId, isEnabled);
            
            Debug.WriteLine($"[PluginMarket] 插件 {pluginId} 已{(isEnabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 设置当前 Profile（供外部调用）
        /// </summary>
        public void SetCurrentProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return;

            // 查找并选中对应的 Profile
            var items = ProfileSelector.ItemsSource as List<ProfileSelectItem>;
            if (items == null) return;

            var index = items.FindIndex(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                ProfileSelector.SelectedIndex = index;
            }
        }

        /// <summary>
        /// 刷新 Profile 列表（供外部调用）
        /// </summary>
        public void RefreshProfileList()
        {
            var currentId = _currentProfileId;
            LoadProfileList();
            
            // 尝试恢复之前的选择
            if (!string.IsNullOrEmpty(currentId))
            {
                SetCurrentProfile(currentId);
            }
        }
    }
}
