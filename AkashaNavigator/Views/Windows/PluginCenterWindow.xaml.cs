using System;
using System.Windows;
using System.Windows.Input;
using AkashaNavigator.Helpers;
using AkashaNavigator.ViewModels.Windows;
using AkashaNavigator.Views.Pages;

namespace AkashaNavigator.Views.Windows
{
/// <summary>
/// 插件中心窗口 - 统一管理所有插件的安装、卸载和配置
/// </summary>
public partial class PluginCenterWindow : AnimatedWindow
{
    private readonly PluginCenterViewModel _viewModel;
    private readonly MyProfilesPage _myProfilesPage;
    private readonly ProfileMarketPage _profileMarketPage;
    private readonly InstalledPluginsPage _installedPluginsPage;
    private readonly AvailablePluginsPage _availablePluginsPage;

    public PluginCenterWindow(PluginCenterViewModel viewModel, MyProfilesPage myProfilesPage,
                              ProfileMarketPage profileMarketPage, InstalledPluginsPage installedPluginsPage,
                              AvailablePluginsPage availablePluginsPage)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _myProfilesPage = myProfilesPage ?? throw new ArgumentNullException(nameof(myProfilesPage));
        _profileMarketPage = profileMarketPage ?? throw new ArgumentNullException(nameof(profileMarketPage));
        _installedPluginsPage = installedPluginsPage ?? throw new ArgumentNullException(nameof(installedPluginsPage));
        _availablePluginsPage = availablePluginsPage ?? throw new ArgumentNullException(nameof(availablePluginsPage));

        InitializeComponent();
        DataContext = _viewModel;

        LoadPages();
        _profileMarketPage.NavigateToMyProfilesRequested += OnNavigateToMyProfilesRequested;
        UpdatePageVisibility(_viewModel.CurrentPage);

        // 订阅 ViewModel 的 PropertyChanged 事件，处理页面显示切换
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.CurrentPage))
            {
                UpdatePageVisibility(_viewModel.CurrentPage);
            }
        };

        Closed += (_, _) => DisposePages();
    }

    /// <summary>
    /// 加载所有 Pages
    /// </summary>
    private void LoadPages()
    {
        ContentArea.Children.Add(_myProfilesPage);
        ContentArea.Children.Add(_profileMarketPage);
        ContentArea.Children.Add(_installedPluginsPage);
        ContentArea.Children.Add(_availablePluginsPage);
    }

    private void OnNavigateToMyProfilesRequested(object? sender, EventArgs e)
    {
        _viewModel.NavigateToMyProfilesCommand.Execute(null);
    }

    /// <summary>
    /// UI 逻辑：页面显示切换（保留在 Code-behind，因为涉及 Visibility 操作）
    /// </summary>
    private void UpdatePageVisibility(PluginCenterPageType currentPage)
    {
        _myProfilesPage.Visibility =
            currentPage == PluginCenterPageType.MyProfiles ? Visibility.Visible : Visibility.Collapsed;
        _profileMarketPage.Visibility =
            currentPage == PluginCenterPageType.ProfileMarket ? Visibility.Visible : Visibility.Collapsed;
        _installedPluginsPage.Visibility =
            currentPage == PluginCenterPageType.InstalledPlugins ? Visibility.Visible : Visibility.Collapsed;
        _availablePluginsPage.Visibility =
            currentPage == PluginCenterPageType.AvailablePlugins ? Visibility.Visible : Visibility.Collapsed;

        RefreshPage(currentPage);
    }

    /// <summary>
    /// 刷新窗口中实际显示的 Page 实例。
    /// PageViewModel 为 Transient，不能通过窗口 ViewModel 另行解析后刷新。
    /// </summary>
    private void RefreshPage(PluginCenterPageType page)
    {
        switch (page)
        {
        case PluginCenterPageType.MyProfiles:
            _myProfilesPage.RefreshProfileList();
            break;
        case PluginCenterPageType.ProfileMarket:
            _ = _profileMarketPage.RefreshProfilesAsync();
            break;
        case PluginCenterPageType.InstalledPlugins:
            _installedPluginsPage.CheckAndRefreshPluginList();
            break;
        case PluginCenterPageType.AvailablePlugins:
            _availablePluginsPage.RefreshPluginList();
            break;
        }
    }

    /// <summary>
    /// UI 逻辑：标题栏拖动（保持不变）
    /// </summary>
    private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    /// <summary>
    /// UI 逻辑：关闭按钮（保持不变）
    /// </summary>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 公开方法供外部调用
    /// </summary>
    public void NavigateToInstalledPlugins()
    {
        _viewModel.NavigateToInstalledPluginsCommand.Execute(null);
    }

    /// <summary>
    /// 公开方法供外部调用
    /// </summary>
    public void NavigateToProfileMarket()
    {
        _viewModel.NavigateToProfileMarketCommand.Execute(null);
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    public void RefreshCurrentPage()
    {
        RefreshPage(_viewModel.CurrentPage);
    }

    private void DisposePages()
    {
        _profileMarketPage.NavigateToMyProfilesRequested -= OnNavigateToMyProfilesRequested;
        (_myProfilesPage as IDisposable)?.Dispose();
        (_profileMarketPage as IDisposable)?.Dispose();
        (_installedPluginsPage as IDisposable)?.Dispose();
        (_availablePluginsPage as IDisposable)?.Dispose();
    }
}
}
