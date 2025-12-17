using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// Profile 创建对话框
    /// </summary>
    public partial class ProfileCreateDialog : AnimatedWindow
    {
        #region Properties

        /// <summary>
        /// 是否确认创建
        /// </summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>
        /// 创建的 Profile ID
        /// </summary>
        public string? ProfileId { get; private set; }

        /// <summary>
        /// Profile 名称
        /// </summary>
        public string ProfileName { get; private set; } = string.Empty;

        /// <summary>
        /// Profile 图标
        /// </summary>
        public string ProfileIcon { get; private set; } = "📦";

        /// <summary>
        /// 选中的插件 ID 列表
        /// </summary>
        public List<string> SelectedPluginIds { get; private set; } = new();

        #endregion

        #region Fields

        private readonly List<PluginSelectorItem> _pluginItems;
        private string _selectedIcon = "📦";

        #endregion

        #region Constructor

        public ProfileCreateDialog()
        {
            InitializeComponent();

            // 初始化图标选择器
            InitializeIconSelector();

            // 加载已安装插件列表
            var installedPlugins = PluginLibrary.Instance.GetInstalledPlugins();
            _pluginItems = installedPlugins.Select(p => new PluginSelectorItem
            {
                Id = p.Id,
                Name = p.Name,
                Version = p.Version,
                Description = p.Description,
                IsSelected = false
            }).ToList();

            // 监听选择变化
            foreach (var item in _pluginItems)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            // 设置插件列表
            if (_pluginItems.Count > 0)
            {
                PluginList.ItemsSource = _pluginItems;
                NoPluginsText.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoPluginsText.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Icon Selector

        /// <summary>
        /// 初始化图标选择器
        /// </summary>
        private void InitializeIconSelector()
        {
            var icons = ProfileManager.ProfileIcons;
            bool isFirst = true;

            foreach (var icon in icons)
            {
                var radioButton = new RadioButton
                {
                    Content = icon,
                    FontSize = 16,
                    GroupName = "IconGroup",
                    Tag = icon,
                    IsChecked = isFirst
                };
                radioButton.Style = (Style)FindResource("IconButtonStyle");
                radioButton.Checked += IconButton_Checked;

                IconPanel.Children.Add(radioButton);

                if (isFirst)
                {
                    _selectedIcon = icon;
                    isFirst = false;
                }
            }
        }

        private void IconButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string icon)
            {
                _selectedIcon = icon;
            }
        }

        #endregion


        #region Event Handlers

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 插件选择变化时可以更新 UI（如果需要）
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 更新占位符可见性
            NamePlaceholder.Visibility = string.IsNullOrEmpty(TxtName.Text) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // 清除错误提示
            TxtError.Visibility = Visibility.Collapsed;

            // 更新创建按钮状态
            UpdateCreateButton();
        }

        private void PluginItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pluginId)
            {
                var item = _pluginItems.FirstOrDefault(i => i.Id == pluginId);
                if (item != null)
                {
                    item.IsSelected = !item.IsSelected;
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            CloseWithAnimation();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            CloseWithAnimation();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (!ValidateInput())
            {
                return;
            }

            // 获取输入值
            ProfileName = TxtName.Text.Trim();
            ProfileIcon = _selectedIcon;
            SelectedPluginIds = _pluginItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            // 生成 Profile ID
            var generatedId = ProfileManager.Instance.GenerateProfileId(ProfileName);

            // 检查 ID 是否已存在
            if (ProfileManager.Instance.ProfileIdExists(generatedId))
            {
                ShowError("已存在同名 Profile");
                return;
            }

            // 创建 Profile
            var result = ProfileManager.Instance.CreateProfile(
                generatedId, 
                ProfileName, 
                ProfileIcon, 
                SelectedPluginIds);

            if (result.IsSuccess)
            {
                ProfileId = result.ProfileId;
                IsConfirmed = true;
                DialogResult = true;
                CloseWithAnimation();
            }
            else
            {
                ShowError(result.ErrorMessage ?? "创建失败");
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            var name = TxtName.Text?.Trim();

            // 检查名称是否为空
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Profile 名称不能为空");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 更新创建按钮状态
        /// </summary>
        private void UpdateCreateButton()
        {
            BtnCreate.IsEnabled = !string.IsNullOrWhiteSpace(TxtName.Text);
        }

        #endregion
    }
}
