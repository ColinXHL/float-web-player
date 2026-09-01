using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;
using AkashaNavigator.ViewModels.Dialogs;
using AkashaNavigator.ViewModels.Pages;
using AkashaNavigator.Views.Dialogs;

namespace AkashaNavigator.Views.Pages
{
/// <summary>
/// Profile 市场页面 - 浏览和安装市场 Profile
/// </summary>
public partial class ProfileMarketPage : System.Windows.Controls.UserControl, IDisposable
{
    private readonly ProfileMarketPageViewModel _viewModel;
    private readonly IDialogFactory _dialogFactory;
    private readonly Func<MarketplaceProfileDetailDialogViewModel> _detailDialogViewModelFactory;
    private bool _disposed;

    /// <summary>
    /// DI容器注入的构造函数
    /// </summary>
    public ProfileMarketPage(ProfileMarketPageViewModel viewModel, IDialogFactory dialogFactory,
                             Func<MarketplaceProfileDetailDialogViewModel> detailDialogViewModelFactory)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
        _detailDialogViewModelFactory =
            detailDialogViewModelFactory ?? throw new ArgumentNullException(nameof(detailDialogViewModelFactory));
        InitializeComponent();

        DataContext = _viewModel;
        Loaded += ProfileMarketPage_Loaded;

        // 订阅 ViewModel 的事件
        _viewModel.ManageSourcesRequested += OnManageSourcesRequested;
        _viewModel.NavigateToMyProfilesRequested += OnNavigateToMyProfilesRequested;
        _viewModel.ShowProfileDetailsRequested += OnShowProfileDetailsRequested;
        _viewModel.UninstallProfileRequested += OnUninstallProfileRequested;
    }

    private void ProfileMarketPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 首次加载时获取数据
        _ = _viewModel.LoadProfilesAsync();
    }

    /// <summary>
    /// 重新加载市场数据（公共方法供插件中心页面切换时调用）。
    /// </summary>
    public Task RefreshProfilesAsync()
    {
        return _viewModel.LoadProfilesAsync();
    }

    public event EventHandler? NavigateToMyProfilesRequested;

    private void OnNavigateToMyProfilesRequested(object? sender, EventArgs e)
    {
        NavigateToMyProfilesRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 处理订阅源管理请求
    /// </summary>
    private void OnManageSourcesRequested(object? sender, EventArgs e)
    {
        var dialog = _dialogFactory.CreateSubscriptionSourceDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            // 订阅源变更后刷新列表
            _ = _viewModel.LoadProfilesAsync();
        }
    }

    /// <summary>
    /// 处理显示 Profile 详情请求
    /// </summary>
    private void OnShowProfileDetailsRequested(object? sender, MarketplaceProfileViewModel vm)
    {
        var dialogViewModel = _detailDialogViewModelFactory();
        var dialog = new MarketplaceProfileDetailDialog(dialogViewModel, vm.Profile);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true && dialog.ShouldInstall)
        {
            if (dialog.Action == MarketplaceProfileDetailAction.Update)
            {
                _ = _viewModel.UpdateCommand.ExecuteAsync(vm);
            }
            else
            {
                _ = _viewModel.InstallCommand.ExecuteAsync(vm);
            }
        }
    }

    /// <summary>
    /// 处理卸载 Profile 请求（显示卸载对话框）
    /// </summary>
    private void OnUninstallProfileRequested(object? sender, ProfileUninstallEventArgs args)
    {
        var pluginItems = new List<PluginUninstallItem>();

        foreach (var pluginId in args.UniquePluginIds)
        {
            var pluginInfo = _viewModel.GetInstalledPluginInfo(pluginId);
            pluginItems.Add(new PluginUninstallItem {
                PluginId = pluginId, Name = pluginInfo?.Name ?? pluginId,
                Description = pluginInfo?.Description ?? string.Empty,
                IsSelected = true // 默认选中
            });
        }

        var dialog = _dialogFactory.CreatePluginUninstallDialog(args.ProfileName, pluginItems);
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() != true || !dialog.Confirmed)
        {
            // 用户取消
            args.Confirmed = false;
            return;
        }

        args.Confirmed = true;
        args.SelectedPluginIds = dialog.SelectedPluginIds;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _viewModel.ManageSourcesRequested -= OnManageSourcesRequested;
        _viewModel.NavigateToMyProfilesRequested -= OnNavigateToMyProfilesRequested;
        _viewModel.ShowProfileDetailsRequested -= OnShowProfileDetailsRequested;
        _viewModel.UninstallProfileRequested -= OnUninstallProfileRequested;
        _viewModel.Dispose();
        _disposed = true;
    }
}
}
