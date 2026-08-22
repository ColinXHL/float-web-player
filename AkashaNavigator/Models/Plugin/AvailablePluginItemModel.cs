using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AkashaNavigator.Models.Plugin
{
    /// <summary>
    /// 可用插件项数据模型
    /// 用于 AvailablePluginsPage 的 ItemsControl 数据绑定
    /// </summary>
    public partial class AvailablePluginItemModel : ObservableObject
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本号
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 插件作者
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// 源代码目录（用于安装）
        /// </summary>
        public string SourceDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 是否来自远程 Manifest。
        /// </summary>
        public bool IsRemote { get; set; }

        public string DistributionType { get; set; } = string.Empty;

        public bool IsRepositoryAvailable { get; set; } = true;

        /// <summary>
        /// 已安装版本；未安装时为空。
        /// </summary>
        public string InstalledVersion { get; set; } = string.Empty;

        public string InstalledVersionText { get; set; } = string.Empty;

        /// <summary>
        /// 远程插件包大小显示文本。
        /// </summary>
        public string PackageSizeText { get; set; } = string.Empty;

        /// <summary>
        /// 是否有描述
        /// </summary>
        public bool HasDescription { get; set; }

        /// <summary>
        /// 是否有作者
        /// </summary>
        public bool HasAuthor { get; set; }

        /// <summary>
        /// 描述可见性
        /// </summary>
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 作者可见性
        /// </summary>
        public Visibility HasAuthorVisibility => HasAuthor ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 插件是否已安装
        /// </summary>
        [ObservableProperty]
        private bool _isInstalled;

        /// <summary>
        /// 是否有远程更新。
        /// </summary>
        [ObservableProperty]
        private bool _hasUpdate;

        /// <summary>
        /// 是否正在安装或更新插件。
        /// </summary>
        [ObservableProperty]
        private bool _isInstalling;

        /// <summary>
        /// 安装过程中的下载进度百分比。
        /// </summary>
        [ObservableProperty]
        private double _installProgress;

        /// <summary>
        /// 安装状态文本。
        /// </summary>
        [ObservableProperty]
        private string _installStatus = string.Empty;

        /// <summary>
        /// 当前阶段没有可量化进度时显示循环进度。
        /// </summary>
        [ObservableProperty]
        private bool _isInstallProgressIndeterminate;

        /// <summary>
        /// 当前下载源文本。
        /// </summary>
        [ObservableProperty]
        private string _selectedSourceText = string.Empty;

        [ObservableProperty]
        private bool _isSubscribed;

        public bool IsRepositoryDistribution =>
            string.Equals(
                DistributionType,
                AppConstants.PluginDistributionRepository,
                StringComparison.Ordinal);

        public bool IsCatalogDistribution =>
            IsRepositoryDistribution ||
            string.Equals(
                DistributionType,
                AppConstants.PluginDistributionRelease,
                StringComparison.Ordinal);

        private bool CanUseCatalogEntry =>
            !IsCatalogDistribution || IsRepositoryAvailable;

        public Visibility AvailableTagVisibility =>
            CanUseCatalogEntry && !IsInstalled && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility InstalledTagVisibility =>
            CanUseCatalogEntry && IsInstalled && !HasUpdate && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility UpdateTagVisibility =>
            CanUseCatalogEntry && IsInstalled && HasUpdate && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility RemovedTagVisibility =>
            IsCatalogDistribution && !IsRepositoryAvailable
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility RemoteInfoVisibility =>
            IsRemote ? Visibility.Visible : Visibility.Collapsed;

        public Visibility InstallationVisibility =>
            IsInstalling ? Visibility.Visible : Visibility.Collapsed;

        public Visibility InstallButtonVisibility =>
            IsRepositoryAvailable && !IsInstalled && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility UpdateButtonVisibility =>
            IsRepositoryAvailable && IsInstalled && HasUpdate && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility UninstallButtonVisibility =>
            IsInstalled && !HasUpdate && !IsInstalling ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CancelButtonVisibility =>
            IsInstalling && IsRemote ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SubscribeButtonVisibility =>
            IsCatalogDistribution &&
            IsRepositoryAvailable &&
            !IsSubscribed &&
            !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility UnsubscribeButtonVisibility =>
            IsCatalogDistribution && IsSubscribed && !IsInstalling
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility SubscriptionStatusVisibility =>
            IsCatalogDistribution && IsSubscribed
                ? Visibility.Visible
                : Visibility.Collapsed;

        partial void OnIsInstalledChanged(bool value)
        {
            NotifyStateVisibilities();
        }

        partial void OnHasUpdateChanged(bool value)
        {
            NotifyStateVisibilities();
        }

        partial void OnIsInstallingChanged(bool value)
        {
            NotifyStateVisibilities();
        }

        partial void OnIsSubscribedChanged(bool value)
        {
            NotifyStateVisibilities();
        }

        private void NotifyStateVisibilities()
        {
            OnPropertyChanged(nameof(AvailableTagVisibility));
            OnPropertyChanged(nameof(InstalledTagVisibility));
            OnPropertyChanged(nameof(UpdateTagVisibility));
            OnPropertyChanged(nameof(InstallationVisibility));
            OnPropertyChanged(nameof(InstallButtonVisibility));
            OnPropertyChanged(nameof(UpdateButtonVisibility));
            OnPropertyChanged(nameof(UninstallButtonVisibility));
            OnPropertyChanged(nameof(CancelButtonVisibility));
            OnPropertyChanged(nameof(SubscribeButtonVisibility));
            OnPropertyChanged(nameof(UnsubscribeButtonVisibility));
            OnPropertyChanged(nameof(SubscriptionStatusVisibility));
        }
    }
}
