using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// SettingsWindow - 设置窗口
    /// </summary>
    public partial class SettingsWindow : AnimatedWindow
    {
        #region Fields

        private AppConfig _config;
        private bool _isInitializing = true;
        private TextBox? _currentHotkeyTextBox;
        private readonly Dictionary<TextBox, uint> _hotkeyValues = new();
        private readonly Dictionary<TextBox, Models.ModifierKeys> _hotkeyModifiers = new();
        private ImeHelper.ImeState _savedImeState;

        #endregion

        #region Constructor

        public SettingsWindow()
        {
            InitializeComponent();
            _config = ConfigService.Instance.Config;
            LoadSettings();
            _isInitializing = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载设置到 UI
        /// </summary>
        private void LoadSettings()
        {
            // 基础设置
            SeekSecondsSlider.Value = _config.SeekSeconds;
            SeekSecondsValue.Text = $"{_config.SeekSeconds}s";
            OpacitySlider.Value = _config.DefaultOpacity * 100;
            OpacityValue.Text = $"{(int)(_config.DefaultOpacity * 100)}%";

            // 窗口行为
            EdgeSnapCheckBox.IsChecked = _config.EnableEdgeSnap;
            SnapThresholdSlider.Value = _config.SnapThreshold;
            SnapThresholdValue.Text = $"{_config.SnapThreshold}px";

            // 快捷键
            LoadHotkey(HotkeySeekBackward, _config.HotkeySeekBackward, _config.HotkeySeekBackwardMod);
            LoadHotkey(HotkeySeekForward, _config.HotkeySeekForward, _config.HotkeySeekForwardMod);
            LoadHotkey(HotkeyTogglePlay, _config.HotkeyTogglePlay, _config.HotkeyTogglePlayMod);
            LoadHotkey(HotkeyDecreaseOpacity, _config.HotkeyDecreaseOpacity, _config.HotkeyDecreaseOpacityMod);
            LoadHotkey(HotkeyIncreaseOpacity, _config.HotkeyIncreaseOpacity, _config.HotkeyIncreaseOpacityMod);
            LoadHotkey(HotkeyToggleClickThrough, _config.HotkeyToggleClickThrough, _config.HotkeyToggleClickThroughMod);

            // 配置 (Profile)
            LoadProfileList();
        }

        /// <summary>
        /// 加载单个快捷键到输入框
        /// </summary>
        private void LoadHotkey(TextBox textBox, uint vkCode, Models.ModifierKeys modifiers)
        {
            textBox.Text = Win32Helper.GetHotkeyDisplayName(vkCode, modifiers);
            _hotkeyValues[textBox] = vkCode;
            _hotkeyModifiers[textBox] = modifiers;
        }

        /// <summary>
        /// 从 UI 读取设置
        /// </summary>
        private void SaveSettingsToConfig()
        {
            // 基础设置
            _config.SeekSeconds = (int)SeekSecondsSlider.Value;
            _config.DefaultOpacity = OpacitySlider.Value / 100.0;
            
            // 窗口行为
            _config.EnableEdgeSnap = EdgeSnapCheckBox.IsChecked ?? true;
            _config.SnapThreshold = (int)SnapThresholdSlider.Value;
            
            // 快捷键
            SaveHotkey(HotkeySeekBackward, v => _config.HotkeySeekBackward = v, m => _config.HotkeySeekBackwardMod = m);
            SaveHotkey(HotkeySeekForward, v => _config.HotkeySeekForward = v, m => _config.HotkeySeekForwardMod = m);
            SaveHotkey(HotkeyTogglePlay, v => _config.HotkeyTogglePlay = v, m => _config.HotkeyTogglePlayMod = m);
            SaveHotkey(HotkeyDecreaseOpacity, v => _config.HotkeyDecreaseOpacity = v, m => _config.HotkeyDecreaseOpacityMod = m);
            SaveHotkey(HotkeyIncreaseOpacity, v => _config.HotkeyIncreaseOpacity = v, m => _config.HotkeyIncreaseOpacityMod = m);
            SaveHotkey(HotkeyToggleClickThrough, v => _config.HotkeyToggleClickThrough = v, m => _config.HotkeyToggleClickThroughMod = m);
        }

        /// <summary>
        /// 保存单个快捷键
        /// </summary>
        private void SaveHotkey(TextBox textBox, Action<uint> setKey, Action<Models.ModifierKeys> setMod)
        {
            if (_hotkeyValues.TryGetValue(textBox, out var vk))
                setKey(vk);
            if (_hotkeyModifiers.TryGetValue(textBox, out var mod))
                setMod(mod);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 标题栏拖动
        /// </summary>
        private new void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            base.TitleBar_MouseLeftButtonDown(sender, e);
        }

        /// <summary>
        /// 快进秒数滑块值变化
        /// </summary>
        private void SeekSecondsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            SeekSecondsValue.Text = $"{(int)SeekSecondsSlider.Value}s";
        }

        /// <summary>
        /// 透明度滑块值变化
        /// </summary>
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            OpacityValue.Text = $"{(int)OpacitySlider.Value}%";
        }

        /// <summary>
        /// 吸附阈值滑块值变化
        /// </summary>
        private void SnapThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            SnapThresholdValue.Text = $"{(int)SnapThresholdSlider.Value}px";
        }

        /// <summary>
        /// 打开配置文件夹
        /// </summary>
        private void BtnOpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = ProfileManager.Instance.DataDirectory;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// 打开插件中心
        /// </summary>
        private void BtnOpenPluginCenter_Click(object sender, RoutedEventArgs e)
        {
            var pluginCenter = new PluginCenterWindow();
            pluginCenter.Owner = this;
            pluginCenter.ShowDialog();
            
            // 插件中心关闭后刷新 Profile 列表（可能有变化）
            _isInitializing = true;
            ProfileManager.Instance.ReloadProfiles();
            LoadProfileList();
            _isInitializing = false;
        }

        /// <summary>
        /// 重置按钮 - 恢复默认设置
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            
            // 重置基础设置
            SeekSecondsSlider.Value = AppConstants.DefaultSeekSeconds;
            SeekSecondsValue.Text = $"{AppConstants.DefaultSeekSeconds}s";
            OpacitySlider.Value = AppConstants.MaxOpacity * 100;
            OpacityValue.Text = $"{(int)(AppConstants.MaxOpacity * 100)}%";
            
            // 重置窗口行为
            EdgeSnapCheckBox.IsChecked = true;
            SnapThresholdSlider.Value = AppConstants.SnapThreshold;
            SnapThresholdValue.Text = $"{AppConstants.SnapThreshold}px";
            
            // 重置快捷键
            var defaultConfig = new AppConfig();
            LoadHotkey(HotkeySeekBackward, defaultConfig.HotkeySeekBackward, defaultConfig.HotkeySeekBackwardMod);
            LoadHotkey(HotkeySeekForward, defaultConfig.HotkeySeekForward, defaultConfig.HotkeySeekForwardMod);
            LoadHotkey(HotkeyTogglePlay, defaultConfig.HotkeyTogglePlay, defaultConfig.HotkeyTogglePlayMod);
            LoadHotkey(HotkeyDecreaseOpacity, defaultConfig.HotkeyDecreaseOpacity, defaultConfig.HotkeyDecreaseOpacityMod);
            LoadHotkey(HotkeyIncreaseOpacity, defaultConfig.HotkeyIncreaseOpacity, defaultConfig.HotkeyIncreaseOpacityMod);
            LoadHotkey(HotkeyToggleClickThrough, defaultConfig.HotkeyToggleClickThrough, defaultConfig.HotkeyToggleClickThroughMod);
            
            _isInitializing = false;
        }

        /// <summary>
        /// 快捷键输入框获得焦点
        /// </summary>
        private void Hotkey_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _currentHotkeyTextBox = textBox;
                textBox.Text = "按下新快捷键...";
                
                // 切换到英文输入模式（需求 2.1）
                _savedImeState = ImeHelper.SwitchToEnglish(this);
            }
        }

        /// <summary>
        /// 快捷键输入框失去焦点
        /// </summary>
        private void Hotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && _hotkeyValues.TryGetValue(textBox, out var vkCode))
            {
                var modifiers = _hotkeyModifiers.TryGetValue(textBox, out var m) ? m : Models.ModifierKeys.None;
                textBox.Text = Win32Helper.GetHotkeyDisplayName(vkCode, modifiers);
            }
            _currentHotkeyTextBox = null;
            
            // 恢复之前的输入法状态（需求 2.2, 2.3）
            ImeHelper.RestoreImeState(_savedImeState);
        }

        /// <summary>
        /// 快捷键按键捕获
        /// </summary>
        private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
            e.Handled = true;
            
            // 忽略修饰键本身
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System)
            {
                return;
            }
            
            // 获取虚拟键码
            var vkCode = (uint)KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);
            
            // 获取当前修饰键状态
            var modifiers = Models.ModifierKeys.None;
            if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                modifiers |= Models.ModifierKeys.Ctrl;
            if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
                modifiers |= Models.ModifierKeys.Alt;
            if (Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                modifiers |= Models.ModifierKeys.Shift;
            
            // 更新显示和存储
            textBox.Text = Win32Helper.GetHotkeyDisplayName(vkCode, modifiers);
            _hotkeyValues[textBox] = vkCode;
            _hotkeyModifiers[textBox] = modifiers;
            
            // 移动焦点到下一个控件
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }

        /// <summary>
        /// 保存按钮
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsToConfig();
            ConfigService.Instance.UpdateConfig(_config);
            CloseWithAnimation();
        }

        /// <summary>
        /// 加载 Profile 列表到 ComboBox
        /// </summary>
        private void LoadProfileList()
        {
            var profiles = ProfileManager.Instance.InstalledProfiles;
            ProfileComboBox.ItemsSource = profiles;
            
            // 选中当前 Profile
            var currentProfile = ProfileManager.Instance.CurrentProfile;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ProfileComboBox.SelectedIndex = i;
                    break;
                }
            }
            
            // 更新取消订阅按钮状态
            UpdateUnsubscribeButtonState();
        }

        /// <summary>
        /// 更新取消订阅按钮状态
        /// </summary>
        private void UpdateUnsubscribeButtonState()
        {
            if (ProfileComboBox.SelectedItem is GameProfile profile)
            {
                // 默认 Profile 不能取消订阅
                BtnUnsubscribeProfile.IsEnabled = !profile.Id.Equals("default", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Profile 选择变化
        /// </summary>
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            
            if (ProfileComboBox.SelectedItem is GameProfile selectedProfile)
            {
                var currentProfile = ProfileManager.Instance.CurrentProfile;
                
                // 如果选择了不同的 Profile，切换
                if (!selectedProfile.Id.Equals(currentProfile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    ProfileManager.Instance.SwitchProfile(selectedProfile.Id);
                    Debug.WriteLine($"[Settings] 已切换到配置: {selectedProfile.Name}");
                }
                
                // 更新取消订阅按钮状态
                UpdateUnsubscribeButtonState();
            }
        }

        /// <summary>
        /// 取消订阅 Profile 按钮点击
        /// </summary>
        private async void BtnUnsubscribeProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is not GameProfile selectedProfile)
                return;
            
            // 不能删除默认 Profile
            if (selectedProfile.Id.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                NotificationService.Instance.Info("不能取消订阅默认配置。", "提示");
                return;
            }
            
            // 显示确认对话框
            var confirmed = await NotificationService.Instance.ConfirmAsync(
                $"确定要取消订阅配置 \"{selectedProfile.Name}\" 吗？\n\n此操作将删除该配置，无法撤销。",
                "确认取消订阅");
            
            if (!confirmed)
                return;
            
            // 调用 ProfileManager.UnsubscribeProfile
            var unsubscribeResult = ProfileManager.Instance.UnsubscribeProfile(selectedProfile.Id);
            
            if (unsubscribeResult.Success)
            {
                Debug.WriteLine($"[Settings] 配置 {selectedProfile.Id} 已取消订阅");
                
                // 刷新 Profile 列表
                _isInitializing = true;
                LoadProfileList();
                _isInitializing = false;
            }
            else
            {
                NotificationService.Instance.Error(
                    unsubscribeResult.ErrorMessage ?? "取消订阅失败",
                    "错误");
                Debug.WriteLine($"[Settings] 取消订阅配置失败: {unsubscribeResult.ErrorMessage}");
            }
        }

        #endregion
    }
}
