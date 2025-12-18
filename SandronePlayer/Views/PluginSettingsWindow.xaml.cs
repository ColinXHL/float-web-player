using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SandronePlayer.Helpers;
using SandronePlayer.Models;
using SandronePlayer.Services;

namespace SandronePlayer.Views
{
    /// <summary>
    /// 插件设置窗口
    /// 用于展示和编辑插件配置
    /// </summary>
    public partial class PluginSettingsWindow : AnimatedWindow
    {
        #region Fields

        private readonly string _pluginId;
        private readonly string _pluginName;
        private readonly string _pluginDirectory;
        private readonly string _configDirectory;
        private readonly PluginConfig _config;
        private readonly SettingsUiDefinition? _settingsDefinition;
        private SettingsUiRenderer? _renderer;
        private bool _hasChanges;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建插件设置窗口
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="pluginName">插件名称</param>
        /// <param name="pluginDirectory">插件源码目录</param>
        /// <param name="configDirectory">插件配置目录</param>
        public PluginSettingsWindow(string pluginId, string pluginName, string pluginDirectory, string configDirectory)
        {
            InitializeComponent();

            _pluginId = pluginId;
            _pluginName = pluginName;
            _pluginDirectory = pluginDirectory;
            _configDirectory = configDirectory;

            // 设置窗口标题
            TitleText.Text = $"{pluginName} - 设置";
            Title = $"{pluginName} - 设置";

            // 加载配置
            var configPath = Path.Combine(configDirectory, AppConstants.PluginConfigFileName);
            _config = PluginConfig.LoadFromFile(configPath, pluginId);

            // 加载设置 UI 定义
            var settingsUiPath = Path.Combine(pluginDirectory, "settings_ui.json");
            _settingsDefinition = SettingsUiDefinition.LoadFromFile(settingsUiPath);

            // 渲染设置界面
            RenderSettings();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 渲染设置界面
        /// </summary>
        private void RenderSettings()
        {
            SettingsContainer.Children.Clear();

            if (_settingsDefinition == null)
            {
                // 没有设置定义，显示提示
                var noSettingsText = new System.Windows.Controls.TextBlock
                {
                    Text = "此插件没有可配置的设置项",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                SettingsContainer.Children.Add(noSettingsText);
                return;
            }

            // 创建渲染器
            _renderer = new SettingsUiRenderer(_settingsDefinition, _config);

            // 监听值变更事件
            _renderer.ValueChanged += OnSettingValueChanged;

            // 监听按钮动作事件
            _renderer.ButtonAction += OnButtonAction;

            // 渲染并添加到容器
            var settingsPanel = _renderer.Render();
            SettingsContainer.Children.Add(settingsPanel);
        }

        /// <summary>
        /// 设置值变更处理
        /// </summary>
        private void OnSettingValueChanged(object? sender, SettingsValueChangedEventArgs e)
        {
            _hasChanges = true;

            // 立即保存配置
            SaveConfig();

            // 通知插件配置已变更
            NotifyPluginConfigChanged(e.Key, e.Value);
        }

        /// <summary>
        /// 按钮动作处理
        /// </summary>
        private void OnButtonAction(object? sender, SettingsButtonActionEventArgs e)
        {
            HandleButtonAction(e.Action);
        }

        /// <summary>
        /// 处理按钮动作
        /// </summary>
        private void HandleButtonAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return;

            // 处理内置动作
            switch (action)
            {
                case SettingsButtonActions.EnterEditMode:
                    // 进入覆盖层编辑模式
                    EnterOverlayEditMode();
                    break;

                case SettingsButtonActions.ResetConfig:
                    // 重置配置
                    ResetToDefaults();
                    break;

                case SettingsButtonActions.OpenPluginFolder:
                    // 打开插件目录
                    OpenPluginFolder();
                    break;

                default:
                    // 自定义动作，通知插件处理
                    NotifyPluginAction(action);
                    break;
            }
        }

        /// <summary>
        /// 保存的父窗口引用（用于编辑模式时隐藏/恢复）
        /// </summary>
        private Window? _parentPluginCenter;
        private Window? _parentSettings;

        /// <summary>
        /// 进入覆盖层编辑模式
        /// </summary>
        private void EnterOverlayEditMode()
        {
            // 隐藏设置窗口，避免阻挡 overlay 编辑
            Hide();

            // 查找并隐藏父级模态窗口（PluginCenterWindow 和 SettingsWindow）
            // 这样才能让用户与 overlay 交互
            HideParentModalWindows();

            // 通过 OverlayManager 获取覆盖层并进入编辑模式
            var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
            if (overlay != null)
            {
                overlay.Show();
                overlay.EnterEditMode();

                // 监听编辑模式退出，恢复设置窗口
                overlay.EditModeExited += OnOverlayEditModeExited;
            }
            else
            {
                // 覆盖层不存在，尝试创建一个临时的用于位置调整
                // 从配置中读取当前位置
                var x = _config.Get("overlay.x", 100.0);
                var y = _config.Get("overlay.y", 100.0);
                var size = _config.Get("overlay.size", 200.0);

                // 创建临时覆盖层用于位置调整
                var options = new OverlayOptions
                {
                    X = x,
                    Y = y,
                    Width = size,
                    Height = size
                };
                var tempOverlay = OverlayManager.Instance.CreateOverlay(_pluginId, options);
                
                // 显示并进入编辑模式
                tempOverlay.Show();
                tempOverlay.EnterEditMode();

                // 监听编辑模式退出，保存位置后销毁临时覆盖层
                tempOverlay.EditModeExited += OnTempOverlayEditModeExited;
            }
        }

        /// <summary>
        /// 隐藏父级模态窗口
        /// </summary>
        private void HideParentModalWindows()
        {
            // 查找 PluginCenterWindow 和 SettingsWindow
            foreach (Window window in Application.Current.Windows)
            {
                if (window is PluginCenterWindow pluginCenter && window.IsVisible)
                {
                    _parentPluginCenter = pluginCenter;
                    pluginCenter.Hide();
                }
                else if (window is SettingsWindow settings && window.IsVisible)
                {
                    _parentSettings = settings;
                    settings.Hide();
                }
            }
        }

        /// <summary>
        /// 恢复父级模态窗口
        /// 恢复顺序：先恢复底层窗口（SettingsWindow），再恢复上层窗口（PluginCenterWindow）
        /// 这样可以保持正确的窗口层级关系
        /// </summary>
        private void RestoreParentModalWindows()
        {
            // 先恢复 SettingsWindow（底层窗口）
            if (_parentSettings != null)
            {
                _parentSettings.Show();
                _parentSettings = null;
            }
            
            // 再恢复 PluginCenterWindow（上层窗口，但不激活）
            // PluginSettingsWindow 会在调用方的 Show() 和 Activate() 中获得焦点
            if (_parentPluginCenter != null)
            {
                _parentPluginCenter.Show();
                // 不调用 Activate()，让 PluginSettingsWindow 保持焦点
                _parentPluginCenter = null;
            }
        }

        /// <summary>
        /// 已有覆盖层编辑模式退出处理
        /// </summary>
        private void OnOverlayEditModeExited(object? sender, EventArgs e)
        {
            if (sender is OverlayWindow overlay)
            {
                // 取消事件订阅
                overlay.EditModeExited -= OnOverlayEditModeExited;

                // 保存位置到配置
                _config.Set("overlay.x", overlay.Left);
                _config.Set("overlay.y", overlay.Top);
                _config.Set("overlay.size", overlay.Width);
                SaveConfig();

                // 刷新 UI 显示
                _renderer?.RefreshValues();

                // 通知插件配置已变更
                NotifyPluginConfigChanged(null, null);

                // 恢复父级模态窗口
                RestoreParentModalWindows();

                // 恢复设置窗口显示
                Show();
                Activate();
            }
        }

        /// <summary>
        /// 临时覆盖层编辑模式退出处理
        /// </summary>
        private void OnTempOverlayEditModeExited(object? sender, EventArgs e)
        {
            if (sender is OverlayWindow overlay)
            {
                // 取消事件订阅
                overlay.EditModeExited -= OnTempOverlayEditModeExited;

                // 保存位置到配置
                _config.Set("overlay.x", overlay.Left);
                _config.Set("overlay.y", overlay.Top);
                _config.Set("overlay.size", overlay.Width);
                SaveConfig();

                // 刷新 UI 显示
                _renderer?.RefreshValues();

                // 通知插件配置已变更
                NotifyPluginConfigChanged(null, null);

                // 销毁临时覆盖层
                OverlayManager.Instance.DestroyOverlay(_pluginId);

                // 恢复父级模态窗口
                RestoreParentModalWindows();

                // 恢复设置窗口显示
                Show();
                Activate();
            }
        }

        /// <summary>
        /// 打开插件目录
        /// </summary>
        private void OpenPluginFolder()
        {
            try
            {
                if (Directory.Exists(_pluginDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _pluginDirectory,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("PluginSettingsWindow", $"打开插件目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        private void ResetToDefaults()
        {
            if (_settingsDefinition?.Sections == null)
                return;

            // 遍历所有设置项，恢复默认值
            foreach (var section in _settingsDefinition.Sections)
            {
                if (section.Items == null) continue;

                foreach (var item in section.Items)
                {
                    ResetItemToDefault(item);
                }
            }

            // 保存配置
            SaveConfig();

            // 刷新 UI
            _renderer?.RefreshValues();

            // 通知插件
            NotifyPluginConfigChanged(null, null);

            _hasChanges = true;
        }

        /// <summary>
        /// 重置单个设置项为默认值
        /// </summary>
        private void ResetItemToDefault(SettingsItem item)
        {
            if (string.IsNullOrEmpty(item.Key))
                return;

            // 获取默认值并设置
            if (item.Default.HasValue)
            {
                var defaultValue = item.Default.Value;
                switch (defaultValue.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        _config.Set(item.Key, defaultValue.GetString());
                        break;
                    case System.Text.Json.JsonValueKind.Number:
                        _config.Set(item.Key, defaultValue.GetDouble());
                        break;
                    case System.Text.Json.JsonValueKind.True:
                    case System.Text.Json.JsonValueKind.False:
                        _config.Set(item.Key, defaultValue.GetBoolean());
                        break;
                }
            }
            else
            {
                // 没有默认值，移除配置项
                _config.Remove(item.Key);
            }

            // 递归处理子项
            if (item.Items != null)
            {
                foreach (var subItem in item.Items)
                {
                    ResetItemToDefault(subItem);
                }
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                var configPath = Path.Combine(_configDirectory, AppConstants.PluginConfigFileName);
                
                // 确保目录存在
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _config.SaveToFile(configPath);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("PluginSettingsWindow", $"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知插件配置已变更
        /// </summary>
        private void NotifyPluginConfigChanged(string? key, object? value)
        {
            try
            {
                // 广播 configChanged 事件
                PluginHost.Instance.BroadcastEvent("configChanged", new
                {
                    pluginId = _pluginId,
                    key = key,
                    value = value
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("PluginSettingsWindow", $"通知插件配置变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知插件执行动作
        /// </summary>
        private void NotifyPluginAction(string action)
        {
            try
            {
                // 广播 settingsAction 事件
                PluginHost.Instance.BroadcastEvent("settingsAction", new
                {
                    pluginId = _pluginId,
                    action = action
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("PluginSettingsWindow", $"通知插件动作失败: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 重置按钮点击
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetToDefaults();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // 如果有变更，通知插件
            if (_hasChanges)
            {
                NotifyPluginConfigChanged(null, null);
            }

            CloseWithAnimation();
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// 检查插件是否有设置界面
        /// </summary>
        /// <param name="pluginDirectory">插件目录</param>
        /// <returns>是否有 settings_ui.json</returns>
        public static bool HasSettingsUi(string pluginDirectory)
        {
            if (string.IsNullOrEmpty(pluginDirectory))
                return false;

            var settingsUiPath = Path.Combine(pluginDirectory, "settings_ui.json");
            return File.Exists(settingsUiPath);
        }

        /// <summary>
        /// 打开插件设置窗口
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="pluginName">插件名称</param>
        /// <param name="pluginDirectory">插件源码目录</param>
        /// <param name="configDirectory">插件配置目录</param>
        /// <param name="owner">父窗口，设置 Owner 以建立父子关系，关闭时焦点自动回到父窗口</param>
        public static void ShowSettings(string pluginId, string pluginName, string pluginDirectory, string configDirectory, Window? owner = null)
        {
            var window = new PluginSettingsWindow(pluginId, pluginName, pluginDirectory, configDirectory);
            
            if (owner != null)
            {
                // 设置 Owner 以建立父子关系
                // 这样关闭设置窗口时焦点会自动回到 owner 窗口
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            window.Show();
        }

        #endregion
    }
}
