using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// Profile 市场页面 - 浏览和安装市场 Profile
    /// </summary>
    public partial class ProfileMarketPage : UserControl
    {
        private bool _isLoading = false;
        private List<MarketplaceProfileViewModel> _allProfiles = new();

        public ProfileMarketPage()
        {
            InitializeComponent();
            Loaded += ProfileMarketPage_Loaded;
        }

        private void ProfileMarketPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 首次加载时获取数据
            if (_allProfiles.Count == 0)
            {
                _ = LoadProfilesAsync();
            }
        }

        /// <summary>
        /// 加载市场 Profile 列表
        /// </summary>
        public async Task LoadProfilesAsync()
        {
            if (_isLoading)
                return;

            _isLoading = true;

            // 显示加载状态
            LoadingText.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Collapsed;
            NoSourcesText.Visibility = Visibility.Collapsed;
            NoResultsText.Visibility = Visibility.Collapsed;
            ProfileList.ItemsSource = null;

            try
            {
                // 获取 Profile 列表（包含内置 Profile 和订阅源 Profile）
                var profiles = await ProfileMarketplaceService.Instance.FetchAvailableProfilesAsync();

                // 如果没有任何 Profile（内置和订阅源都没有）
                if (profiles.Count == 0)
                {
                    LoadingText.Visibility = Visibility.Collapsed;
                    NoSourcesText.Visibility = Visibility.Visible;
                    _isLoading = false;
                    return;
                }

                // 转换为视图模型
                _allProfiles = profiles.Select(p => new MarketplaceProfileViewModel(p)).ToList();

                // 应用过滤
                FilterProfiles();
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"加载失败: {ex.Message}";
                ErrorBorder.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        /// <summary>
        /// 过滤 Profile 列表
        /// </summary>
        private void FilterProfiles()
        {
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            
            List<MarketplaceProfileViewModel> filtered;
            if (string.IsNullOrEmpty(query))
            {
                filtered = _allProfiles;
            }
            else
            {
                var lowerQuery = query.ToLowerInvariant();
                filtered = _allProfiles.Where(p =>
                    (p.Name?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                    (p.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                    (p.TargetGame?.ToLowerInvariant().Contains(lowerQuery) ?? false)
                ).ToList();
            }

            ProfileList.ItemsSource = filtered;
            NoResultsText.Visibility = filtered.Count == 0 && _allProfiles.Count > 0 
                ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 搜索框文本变化
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterProfiles();
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadProfilesAsync();
        }

        /// <summary>
        /// 订阅源管理按钮点击
        /// </summary>
        private void BtnManageSources_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SubscriptionSourceDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                // 订阅源变更后刷新列表
                _ = LoadProfilesAsync();
            }
        }

        /// <summary>
        /// Profile 详情按钮点击
        /// </summary>
        private void BtnProfileDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceProfileViewModel vm)
            {
                var dialog = new MarketplaceProfileDetailDialog(vm.Profile);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true && dialog.ShouldInstall)
                {
                    _ = InstallProfileAsync(vm.Profile);
                }
            }
        }

        /// <summary>
        /// Profile 安装按钮点击
        /// </summary>
        private void BtnProfileInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceProfileViewModel vm)
            {
                _ = InstallProfileAsync(vm.Profile);
            }
        }

        /// <summary>
        /// Profile 卸载按钮点击
        /// </summary>
        private void BtnProfileUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceProfileViewModel vm)
            {
                _ = UninstallProfileWithDialogAsync(vm);
            }
        }

        /// <summary>
        /// 带对话框的 Profile 卸载流程
        /// </summary>
        private async Task UninstallProfileWithDialogAsync(MarketplaceProfileViewModel vm)
        {
            var profileId = vm.Id;
            var profileName = vm.Name;

            // 获取唯一插件列表
            var uniquePluginIds = ProfileMarketplaceService.Instance.GetUniquePlugins(profileId);

            List<string>? pluginsToUninstall = null;

            if (uniquePluginIds.Count > 0)
            {
                // 有唯一插件，显示 PluginUninstallDialog
                var pluginItems = uniquePluginIds.Select(pluginId =>
                {
                    var pluginInfo = PluginLibrary.Instance.GetInstalledPluginInfo(pluginId);
                    return new PluginUninstallItem
                    {
                        PluginId = pluginId,
                        Name = pluginInfo?.Name ?? pluginId,
                        Description = pluginInfo?.Description ?? string.Empty,
                        IsSelected = true // 默认选中
                    };
                }).ToList();

                var dialog = new PluginUninstallDialog(profileName, pluginItems);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() != true || !dialog.Confirmed)
                {
                    // 用户取消
                    return;
                }

                pluginsToUninstall = dialog.SelectedPluginIds;
            }
            else
            {
                // 没有唯一插件，使用 NotificationService 显示确认对话框
                var confirmed = await NotificationService.Instance.ConfirmAsync(
                    $"确定要卸载 Profile \"{profileName}\" 吗？\n\n此操作将删除该 Profile 的配置文件。",
                    "确认卸载");

                if (!confirmed)
                {
                    return;
                }
            }

            // 执行卸载
            await UninstallProfileAsync(vm, pluginsToUninstall);
        }

        /// <summary>
        /// 执行 Profile 卸载
        /// </summary>
        private async Task UninstallProfileAsync(MarketplaceProfileViewModel vm, List<string>? pluginsToUninstall)
        {
            var profileId = vm.Id;
            var profileName = vm.Name;

            // 调用服务执行卸载
            var result = ProfileMarketplaceService.Instance.UninstallProfile(profileId, pluginsToUninstall);

            if (result.IsSuccess)
            {
                // 更新 ViewModel 状态
                vm.IsInstalled = false;

                // 构建成功消息
                var message = $"Profile \"{profileName}\" 已成功卸载。";

                if (result.UninstalledPlugins.Count > 0)
                {
                    message += $"\n\n已卸载 {result.UninstalledPlugins.Count} 个插件。";
                }

                if (result.FailedPlugins.Count > 0)
                {
                    message += $"\n\n⚠ {result.FailedPlugins.Count} 个插件卸载失败：\n";
                    message += string.Join("\n", result.FailedPlugins.Take(5).Select(p => $"  • {p}"));
                    if (result.FailedPlugins.Count > 5)
                    {
                        message += $"\n  ... 等 {result.FailedPlugins.Count} 个";
                    }
                }

                NotificationService.Instance.Success(message, "卸载成功");

                // 刷新列表以更新 UI 状态
                FilterProfiles();
            }
            else
            {
                NotificationService.Instance.Error($"卸载失败: {result.ErrorMessage}", "卸载失败");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 安装 Profile
        /// </summary>
        private async Task InstallProfileAsync(MarketplaceProfile profile)
        {
            // 检测缺失插件
            var missingPlugins = ProfileMarketplaceService.Instance.GetMissingPlugins(profile);
            
            // 检查是否已存在
            bool overwrite = false;
            if (ProfileMarketplaceService.Instance.ProfileExists(profile.Id))
            {
                var confirmed = await NotificationService.Instance.ConfirmAsync(
                    $"Profile \"{profile.Name}\" 已存在。\n\n是否覆盖现有 Profile？",
                    "Profile 已存在");

                if (!confirmed)
                    return;
                
                overwrite = true;
            }
            else if (missingPlugins.Count > 0)
            {
                // 提示缺失插件
                var message = $"即将安装 Profile: {profile.Name}\n\n";
                message += $"⚠ {missingPlugins.Count} 个插件缺失:\n";
                message += string.Join("\n", missingPlugins.Take(5).Select(p => $"  • {p}"));
                if (missingPlugins.Count > 5)
                {
                    message += $"\n  ... 等 {missingPlugins.Count} 个";
                }
                message += "\n\n安装后可以在「我的 Profile」页面一键安装缺失插件。\n\n是否继续？";

                var confirmed = await NotificationService.Instance.ConfirmAsync(message, "确认安装");
                if (!confirmed)
                    return;
            }

            // 执行安装
            var installResult = await ProfileMarketplaceService.Instance.InstallProfileAsync(profile, overwrite);

            if (installResult.IsSuccess)
            {
                var successMessage = $"Profile \"{profile.Name}\" 安装成功！";
                if (installResult.MissingPlugins.Count > 0)
                {
                    successMessage += $"\n\n有 {installResult.MissingPlugins.Count} 个插件缺失，可以在「我的 Profile」页面点击「一键安装缺失插件」进行安装。";
                }
                
                NotificationService.Instance.Success(successMessage, "安装成功");
            }
            else
            {
                NotificationService.Instance.Error($"安装失败: {installResult.ErrorMessage}", "安装失败");
            }
        }
    }

    /// <summary>
    /// 市场 Profile 视图模型
    /// </summary>
    public class MarketplaceProfileViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public MarketplaceProfile Profile { get; }
        private bool _isInstalled;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public MarketplaceProfileViewModel(MarketplaceProfile profile)
        {
            Profile = profile;
            // 初始化时检查安装状态
            _isInstalled = ProfileMarketplaceService.Instance.ProfileExists(profile.Id);
        }

        public string Id => Profile.Id;
        public string Name => Profile.Name;
        public string Description => Profile.Description;
        public string Author => Profile.Author;
        public string TargetGame => Profile.TargetGame;
        public string Version => Profile.Version;
        public int PluginCount => Profile.PluginCount;

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);
        public bool HasTargetGame => !string.IsNullOrWhiteSpace(TargetGame);

        /// <summary>
        /// Profile 是否已安装
        /// </summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged(nameof(IsInstalled));
                    OnPropertyChanged(nameof(CanUninstall));
                    OnPropertyChanged(nameof(IsDefaultAndInstalled));
                    OnPropertyChanged(nameof(ActionButtonText));
                }
            }
        }

        /// <summary>
        /// 是否是默认 Profile
        /// </summary>
        public bool IsDefaultProfile => Id.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 是否可以卸载（已安装且非默认 Profile）
        /// </summary>
        public bool CanUninstall => IsInstalled && !IsDefaultProfile;

        /// <summary>
        /// 是否是已安装的默认 Profile（用于显示"已安装"标签）
        /// </summary>
        public bool IsDefaultAndInstalled => IsInstalled && IsDefaultProfile;

        /// <summary>
        /// 操作按钮文本
        /// </summary>
        public string ActionButtonText => IsInstalled ? "卸载" : "安装";

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
