using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件中心窗口 - 统一管理所有插件的安装、卸载和配置
    /// </summary>
    public partial class PluginCenterWindow : AnimatedWindow
    {
        public PluginCenterWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击切换最大化/还原
                WindowState = WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 导航按钮切换
        /// </summary>
        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton) return;
            
            // 防止在 InitializeComponent 期间触发（控件尚未初始化）
            if (InstalledPluginsPage == null) return;

            // 隐藏所有页面
            InstalledPluginsPage.Visibility = Visibility.Collapsed;
            AvailablePluginsPage.Visibility = Visibility.Collapsed;
            MyProfilesPage.Visibility = Visibility.Collapsed;
            ProfileMarketPage.Visibility = Visibility.Collapsed;

            // 根据选中的导航按钮显示对应页面
            if (radioButton == NavInstalledPlugins)
            {
                InstalledPluginsPage.Visibility = Visibility.Visible;
                InstalledPluginsPage.RefreshPluginList();
            }
            else if (radioButton == NavAvailablePlugins)
            {
                AvailablePluginsPage.Visibility = Visibility.Visible;
                AvailablePluginsPage.RefreshPluginList();
            }
            else if (radioButton == NavMyProfiles)
            {
                MyProfilesPage.Visibility = Visibility.Visible;
                MyProfilesPage.RefreshProfileList();
            }
            else if (radioButton == NavProfileMarket)
            {
                ProfileMarketPage.Visibility = Visibility.Visible;
                _ = ProfileMarketPage.LoadProfilesAsync();
            }
        }

        /// <summary>
        /// 刷新当前页面
        /// </summary>
        public void RefreshCurrentPage()
        {
            if (NavInstalledPlugins.IsChecked == true)
            {
                InstalledPluginsPage.RefreshPluginList();
            }
            else if (NavAvailablePlugins.IsChecked == true)
            {
                AvailablePluginsPage.RefreshPluginList();
            }
        }

        /// <summary>
        /// 导航到已安装插件页面
        /// </summary>
        public void NavigateToInstalledPlugins()
        {
            NavInstalledPlugins.IsChecked = true;
        }

        /// <summary>
        /// 导航到可用插件页面
        /// </summary>
        public void NavigateToAvailablePlugins()
        {
            NavAvailablePlugins.IsChecked = true;
        }
    }
}
