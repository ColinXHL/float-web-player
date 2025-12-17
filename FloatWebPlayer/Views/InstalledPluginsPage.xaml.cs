using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 已安装插件页面 - 显示全局插件库中的所有插件
    /// </summary>
    public partial class InstalledPluginsPage : UserControl
    {
        public InstalledPluginsPage()
        {
            InitializeComponent();
            Loaded += InstalledPluginsPage_Loaded;
        }

        private void InstalledPluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 刷新插件列表（公共方法，不带更新信息）
        /// </summary>
        public void RefreshPluginList()
        {
            RefreshPluginList(null);
        }

        /// <summary>
        /// 获取插件关联的Profile文本
        /// </summary>
        private string GetProfilesText(string pluginId)
        {
            var profiles = PluginAssociationManager.Instance.GetProfilesUsingPlugin(pluginId);
            if (profiles.Count == 0)
                return "无";
            if (profiles.Count <= 3)
                return string.Join(", ", profiles);
            return $"{string.Join(", ", profiles.Take(3))} 等 {profiles.Count} 个";
        }

        /// <summary>
        /// 检查插件是否有设置UI
        /// </summary>
        private bool HasSettingsUi(string pluginId)
        {
            var pluginDir = PluginLibrary.Instance.GetPluginDirectory(pluginId);
            var settingsUiPath = Path.Combine(pluginDir, "settings_ui.json");
            return File.Exists(settingsUiPath);
        }

        /// <summary>
        /// 搜索框文本变化
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 检查更新按钮点击
        /// </summary>
        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            // 调用 PluginLibrary.CheckAllUpdates
            var updates = PluginLibrary.Instance.CheckAllUpdates();

            if (updates.Count == 0)
            {
                // 没有可用更新
                NotificationService.Instance.Show("所有插件都是最新版本", NotificationType.Success);
            }
            else
            {
                // 有可用更新，刷新列表以显示更新信息
                RefreshPluginList(updates);
                NotificationService.Instance.Show($"发现 {updates.Count} 个插件有可用更新", NotificationType.Info);
            }
        }

        /// <summary>
        /// 刷新插件列表（带更新信息）
        /// </summary>
        private void RefreshPluginList(List<UpdateCheckResult>? updateResults = null)
        {
            var plugins = PluginLibrary.Instance.GetInstalledPlugins();
            var searchText = SearchBox?.Text?.ToLower() ?? "";

            // 过滤搜索
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                plugins = plugins.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    (p.Description?.ToLower().Contains(searchText) ?? false)
                ).ToList();
            }

            // 创建更新信息字典
            var updateDict = updateResults?.ToDictionary(u => u.PluginId, u => u) 
                ?? new Dictionary<string, UpdateCheckResult>();

            // 转换为视图模型
            var viewModels = plugins.Select(p =>
            {
                var vm = new InstalledPluginViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Version = p.Version,
                    Description = p.Description,
                    Author = p.Author,
                    ReferenceCount = PluginAssociationManager.Instance.GetPluginReferenceCount(p.Id),
                    ProfilesText = GetProfilesText(p.Id),
                    HasDescription = !string.IsNullOrWhiteSpace(p.Description),
                    HasSettingsUi = HasSettingsUi(p.Id)
                };

                // 设置更新信息
                if (updateDict.TryGetValue(p.Id, out var updateInfo) && updateInfo.HasUpdate)
                {
                    vm.HasUpdate = true;
                    vm.AvailableVersion = updateInfo.AvailableVersion;
                }

                return vm;
            }).ToList();

            PluginList.ItemsSource = viewModels;
            PluginCountText.Text = $"共 {viewModels.Count} 个插件";
            NoPluginsText.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 设置按钮点击
        /// </summary>
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                // 获取插件信息
                var pluginInfo = PluginLibrary.Instance.GetInstalledPluginInfo(pluginId);
                if (pluginInfo == null)
                {
                    NotificationService.Instance.Show($"找不到插件 {pluginId}", NotificationType.Error);
                    return;
                }

                // 获取插件目录
                var pluginDirectory = PluginLibrary.Instance.GetPluginDirectory(pluginId);
                
                // 获取配置目录（使用插件库目录作为配置目录）
                var configDirectory = pluginDirectory;

                // 打开插件设置窗口
                PluginSettingsWindow.ShowSettings(
                    pluginId,
                    pluginInfo.Name,
                    pluginDirectory,
                    configDirectory,
                    Window.GetWindow(this)
                );
            }
        }

        /// <summary>
        /// 添加到Profile按钮点击
        /// </summary>
        private void BtnAddToProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                var dialog = new ProfileSelectorDialog(pluginId);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                {
                    RefreshPluginList();
                }
            }
        }

        /// <summary>
        /// 卸载按钮点击
        /// </summary>
        private void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                // 获取插件名称用于显示
                var pluginInfo = PluginLibrary.Instance.GetInstalledPluginInfo(pluginId);
                var pluginName = pluginInfo?.Name ?? pluginId;

                // 显示卸载确认对话框
                var dialog = new UninstallConfirmDialog(pluginId, pluginName);
                dialog.Owner = Window.GetWindow(this);
                
                if (dialog.ShowDialog() == true && dialog.UninstallSucceeded)
                {
                    RefreshPluginList();
                }
            }
        }

        /// <summary>
        /// 更新按钮点击
        /// </summary>
        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                // 获取插件名称用于显示
                var pluginInfo = PluginLibrary.Instance.GetInstalledPluginInfo(pluginId);
                var pluginName = pluginInfo?.Name ?? pluginId;

                // 调用 PluginLibrary.UpdatePlugin
                var result = PluginLibrary.Instance.UpdatePlugin(pluginId);

                if (result.IsSuccess)
                {
                    // 显示成功通知
                    NotificationService.Instance.Show(
                        $"{pluginName} 已更新到 v{result.NewVersion}",
                        NotificationType.Success);
                    
                    // 刷新列表
                    RefreshPluginList();
                }
                else
                {
                    // 显示失败通知
                    NotificationService.Instance.Show(
                        $"更新 {pluginName} 失败: {result.ErrorMessage}",
                        NotificationType.Error);
                }
            }
        }
    }

    /// <summary>
    /// 已安装插件视图模型
    /// </summary>
    public class InstalledPluginViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public int ReferenceCount { get; set; }
        public string ProfilesText { get; set; } = "无";
        public bool HasDescription { get; set; }
        public bool HasSettingsUi { get; set; }
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasSettingsUiVisibility => HasSettingsUi ? Visibility.Visible : Visibility.Collapsed;

        // 更新相关属性
        /// <summary>
        /// 是否有可用更新
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// 可用的新版本号
        /// </summary>
        public string? AvailableVersion { get; set; }

        /// <summary>
        /// 更新按钮可见性
        /// </summary>
        public Visibility UpdateButtonVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 更新按钮文本
        /// </summary>
        public string UpdateButtonText => $"更新到 v{AvailableVersion}";

        /// <summary>
        /// 更新可用标签可见性
        /// </summary>
        public Visibility UpdateAvailableTagVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 更新可用标签文本
        /// </summary>
        public string UpdateAvailableTagText => $"更新可用 v{AvailableVersion}";
    }
}
