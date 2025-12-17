using System.Windows;
using System.Windows.Controls;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// Profile 编辑对话框
    /// </summary>
    public partial class ProfileEditDialog : AnimatedWindow
    {
        #region Properties

        /// <summary>
        /// 是否确认保存
        /// </summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>
        /// 新的 Profile 名称
        /// </summary>
        public string NewName { get; private set; } = string.Empty;

        /// <summary>
        /// 新的 Profile 图标
        /// </summary>
        public string NewIcon { get; private set; } = "📦";

        #endregion

        #region Fields

        private readonly GameProfile _profile;
        private readonly string _originalName;
        private readonly string _originalIcon;
        private string _selectedIcon;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建 Profile 编辑对话框
        /// </summary>
        /// <param name="profile">要编辑的 Profile</param>
        public ProfileEditDialog(GameProfile profile)
        {
            InitializeComponent();

            _profile = profile;
            _originalName = profile.Name;
            _originalIcon = profile.Icon;
            _selectedIcon = profile.Icon;

            // 初始化图标选择器
            InitializeIconSelector();

            // 预填当前名称
            TxtName.Text = profile.Name;
            NamePlaceholder.Visibility = Visibility.Collapsed;

            // 更新保存按钮状态
            UpdateSaveButton();
        }

        #endregion

        #region Icon Selector

        /// <summary>
        /// 初始化图标选择器
        /// </summary>
        private void InitializeIconSelector()
        {
            var icons = ProfileManager.ProfileIcons;

            foreach (var icon in icons)
            {
                var radioButton = new RadioButton
                {
                    Content = icon,
                    FontSize = 16,
                    GroupName = "IconGroup",
                    Tag = icon,
                    IsChecked = icon == _originalIcon
                };
                radioButton.Style = (Style)FindResource("IconButtonStyle");
                radioButton.Checked += IconButton_Checked;

                IconPanel.Children.Add(radioButton);
            }
        }

        private void IconButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string icon)
            {
                _selectedIcon = icon;
                UpdateSaveButton();
            }
        }

        #endregion

        #region Event Handlers

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 更新占位符可见性
            NamePlaceholder.Visibility = string.IsNullOrEmpty(TxtName.Text) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // 清除错误提示
            TxtError.Visibility = Visibility.Collapsed;

            // 更新保存按钮状态
            UpdateSaveButton();
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (!ValidateInput())
            {
                return;
            }

            // 获取输入值
            NewName = TxtName.Text.Trim();
            NewIcon = _selectedIcon;

            // 更新 Profile
            var success = ProfileManager.Instance.UpdateProfile(_profile.Id, NewName, NewIcon);

            if (success)
            {
                IsConfirmed = true;
                DialogResult = true;
                CloseWithAnimation();
            }
            else
            {
                ShowError("保存失败");
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
        /// 更新保存按钮状态
        /// </summary>
        private void UpdateSaveButton()
        {
            var name = TxtName.Text?.Trim();
            
            // 名称不能为空
            if (string.IsNullOrWhiteSpace(name))
            {
                BtnSave.IsEnabled = false;
                return;
            }

            // 检查是否有变化
            var hasChanges = name != _originalName || _selectedIcon != _originalIcon;
            BtnSave.IsEnabled = hasChanges;
        }

        #endregion
    }
}
