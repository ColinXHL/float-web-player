using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件卸载选择对话框 - 在卸载 Profile 时显示唯一插件列表供用户选择
    /// </summary>
    public partial class PluginUninstallDialog : AnimatedWindow
    {
        private readonly string _profileName;

        /// <summary>
        /// 插件列表（可绑定）
        /// </summary>
        public ObservableCollection<PluginUninstallItem> Plugins { get; }

        /// <summary>
        /// 用户是否确认卸载
        /// </summary>
        public bool Confirmed { get; private set; }

        /// <summary>
        /// 获取用户选择要卸载的插件 ID 列表
        /// </summary>
        public List<string> SelectedPluginIds => Plugins
            .Where(p => p.IsSelected)
            .Select(p => p.PluginId)
            .ToList();

        /// <summary>
        /// 创建插件卸载选择对话框
        /// </summary>
        /// <param name="profileName">Profile 名称（用于显示）</param>
        /// <param name="plugins">唯一插件列表</param>
        public PluginUninstallDialog(string profileName, IEnumerable<PluginUninstallItem> plugins)
        {
            InitializeComponent();
            
            _profileName = profileName;
            Plugins = new ObservableCollection<PluginUninstallItem>(plugins);

            InitializeUI();
            Loaded += PluginUninstallDialog_Loaded;
        }

        /// <summary>
        /// 初始化 UI
        /// </summary>
        private void InitializeUI()
        {
            ProfileNameText.Text = $"确定要卸载 \"{_profileName}\" 吗？";
            PluginList.ItemsSource = Plugins;
        }


        /// <summary>
        /// 窗口加载完成
        /// </summary>
        private void PluginUninstallDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 播放进入动画
            var fadeIn = new DoubleAnimation(0, 1, System.TimeSpan.FromMilliseconds(150));
            var scaleX = new DoubleAnimation(0.96, 1, System.TimeSpan.FromMilliseconds(150));
            var scaleY = new DoubleAnimation(0.96, 1, System.TimeSpan.FromMilliseconds(150));

            fadeIn.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scaleY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            MainContainer.BeginAnimation(OpacityProperty, fadeIn);
            ContainerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            ContainerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
        }

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 全选按钮点击
        /// </summary>
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plugin in Plugins)
            {
                plugin.IsSelected = true;
            }
        }

        /// <summary>
        /// 全不选按钮点击
        /// </summary>
        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var plugin in Plugins)
            {
                plugin.IsSelected = false;
            }
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 确认卸载按钮点击
        /// </summary>
        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
