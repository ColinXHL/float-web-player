using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// Profile 市场页面视图模型
    /// </summary>
    public class ProfileMarketItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> RecommendedPlugins { get; set; } = new();
        public List<string> RecommendedPluginNames { get; set; } = new();
        public bool IsSubscribed { get; set; }
        public bool CanUnsubscribe { get; set; }
        public bool HasRecommendedPlugins => RecommendedPlugins.Count > 0;
    }

    /// <summary>
    /// Profile 市场页面
    /// 显示所有内置 Profile，支持订阅/取消订阅
    /// </summary>
    public partial class ProfileMarketPage : UserControl
    {
        /// <summary>
        /// Profile 订阅状态变化事件
        /// </summary>
        public event EventHandler? SubscriptionChanged;

        public ProfileMarketPage()
        {
            InitializeComponent();
            
            // 加载 Profile 列表
            RefreshProfileList();
        }

        /// <summary>
        /// 刷新 Profile 列表
        /// </summary>
        public void RefreshProfileList()
        {
            var allProfiles = ProfileRegistry.Instance.GetAllProfiles();
            var subscribedProfiles = SubscriptionManager.Instance.GetSubscribedProfiles();

            var items = new List<ProfileMarketItem>();

            foreach (var profile in allProfiles)
            {
                var isSubscribed = subscribedProfiles.Contains(profile.Id, StringComparer.OrdinalIgnoreCase);
                
                // 获取推荐插件的名称
                var pluginNames = new List<string>();
                foreach (var pluginId in profile.RecommendedPlugins)
                {
                    var pluginInfo = PluginRegistry.Instance.GetPlugin(pluginId);
                    pluginNames.Add(pluginInfo?.Name ?? pluginId);
                }

                items.Add(new ProfileMarketItem
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    Icon = profile.Icon,
                    Description = profile.Description,
                    RecommendedPlugins = profile.RecommendedPlugins,
                    RecommendedPluginNames = pluginNames,
                    IsSubscribed = isSubscribed,
                    // 默认 Profile 不能取消订阅
                    CanUnsubscribe = isSubscribed && !profile.Id.Equals("default", StringComparison.OrdinalIgnoreCase)
                });
            }

            ProfileList.ItemsSource = items;
            EmptyMessage.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 订阅按钮点击
        /// </summary>
        private void BtnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string profileId)
                return;

            // 获取 Profile 信息
            var profileInfo = ProfileRegistry.Instance.GetProfile(profileId);
            if (profileInfo == null)
            {
                MessageBox.Show($"未找到 Profile: {profileId}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 显示订阅确认对话框
            var message = $"确定要订阅「{profileInfo.Name}」Profile 吗？";
            
            if (profileInfo.RecommendedPlugins.Count > 0)
            {
                var pluginNames = profileInfo.RecommendedPlugins
                    .Select(id => PluginRegistry.Instance.GetPlugin(id)?.Name ?? id)
                    .ToList();
                message += $"\n\n将自动订阅以下推荐插件：\n  • {string.Join("\n  • ", pluginNames)}";
            }

            var result = MessageBox.Show(
                message,
                "订阅 Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 执行订阅
            var success = SubscriptionManager.Instance.SubscribeProfile(profileId);
            
            if (success)
            {
                Debug.WriteLine($"[ProfileMarket] 成功订阅 Profile: {profileId}");
                RefreshProfileList();
                SubscriptionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    $"订阅 Profile「{profileInfo.Name}」失败",
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
            if (sender is not Button button || button.Tag is not string profileId)
                return;

            // 获取 Profile 信息
            var profileInfo = ProfileRegistry.Instance.GetProfile(profileId);
            if (profileInfo == null)
            {
                MessageBox.Show($"未找到 Profile: {profileId}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 不能取消订阅默认 Profile
            if (profileId.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("不能取消订阅默认配置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 显示确认对话框
            var result = MessageBox.Show(
                $"确定要取消订阅配置「{profileInfo.Name}」吗？\n\n此操作将删除该配置及其所有插件设置，无法撤销。",
                "确认取消订阅",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            // 执行取消订阅
            var unsubscribeResult = SubscriptionManager.Instance.UnsubscribeProfile(profileId);

            if (unsubscribeResult.Success)
            {
                Debug.WriteLine($"[ProfileMarket] 成功取消订阅 Profile: {profileId}");
                RefreshProfileList();
                SubscriptionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                MessageBox.Show(
                    unsubscribeResult.ErrorMessage ?? "取消订阅失败",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}