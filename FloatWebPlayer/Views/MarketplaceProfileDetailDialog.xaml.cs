using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 市场 Profile 详情对话框
    /// </summary>
    public partial class MarketplaceProfileDetailDialog : Window
    {
        private readonly MarketplaceProfile _profile;

        /// <summary>
        /// 是否应该安装
        /// </summary>
        public bool ShouldInstall { get; private set; }

        public MarketplaceProfileDetailDialog(MarketplaceProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            LoadProfileDetails();
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ShouldInstall = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 加载 Profile 详情
        /// </summary>
        private void LoadProfileDetails()
        {
            // 基本信息
            ProfileName.Text = _profile.Name;
            ProfileVersion.Text = $"v{_profile.Version}";
            ProfileDescription.Text = string.IsNullOrWhiteSpace(_profile.Description) 
                ? "暂无描述" : _profile.Description;

            // 目标游戏
            if (string.IsNullOrWhiteSpace(_profile.TargetGame))
            {
                TargetGameBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                TargetGameText.Text = _profile.TargetGame;
            }

            // 元信息
            AuthorText.Text = string.IsNullOrWhiteSpace(_profile.Author) ? "未知" : _profile.Author;
            UpdatedAtText.Text = _profile.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
            PluginCountText.Text = $"{_profile.PluginCount} 个";

            // 插件列表
            var pluginViewModels = _profile.PluginIds
                .Select(id => new PluginStatusViewModel(id))
                .ToList();
            PluginList.ItemsSource = pluginViewModels;
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ShouldInstall = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 安装按钮点击
        /// </summary>
        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            ShouldInstall = true;
            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// 插件状态视图模型
    /// </summary>
    public class PluginStatusViewModel
    {
        public string PluginId { get; }
        public bool IsInstalled { get; }

        public PluginStatusViewModel(string pluginId)
        {
            PluginId = pluginId;
            IsInstalled = PluginLibrary.Instance.IsInstalled(pluginId);
        }

        public string StatusText => IsInstalled ? "已安装" : "缺失";

        public Brush StatusForeground => IsInstalled 
            ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))  // 绿色
            : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)); // 红色

        public Brush StatusBackground => IsInstalled
            ? new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A))  // 深绿背景
            : new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A)); // 深红背景
    }
}
