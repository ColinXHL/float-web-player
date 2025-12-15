using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件设置页面
    /// 支持插件列表显示和声明式设置 UI
    /// </summary>
    public partial class PluginSettingsPage : UserControl
    {
        #region Fields

        private string? _currentPluginId;
        private SettingsUiRenderer? _currentRenderer;
        private SettingsUiDefinition? _currentDefinition;

        #endregion

        #region Constructor

        public PluginSettingsPage()
        {
            InitializeComponent();
            Loaded += PluginSettingsPage_Loaded;
        }

        #endregion

        #region Event Handlers

        private void PluginSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 插件启用/禁用切换
        /// </summary>
        private void PluginToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Tag is string pluginId)
            {
                var isEnabled = toggle.IsChecked ?? false;
                PluginHost.Instance.SetPluginEnabled(pluginId, isEnabled);
                Debug.WriteLine($"[PluginSettings] 插件 {pluginId} 已{(isEnabled ? "启用" : "禁用")}");
            }
        }

        /// <summary>
        /// 取消订阅按钮点击
        /// </summary>
        private void BtnUnsubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                var plugin = PluginHost.Instance.GetPlugin(pluginId);
                var pluginName = plugin?.Manifest.Name ?? pluginId;

                var result = MessageBox.Show(
                    $"确定要取消订阅插件 \"{pluginName}\" 吗？\n\n此操作将停止插件运行并删除插件目录，无法撤销。",
                    "确认取消订阅",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                var unsubscribeResult = PluginHost.Instance.UnsubscribePlugin(pluginId);

                if (unsubscribeResult.Success)
                {
                    Debug.WriteLine($"[PluginSettings] 插件 {pluginId} 已取消订阅");
                    RefreshPluginList();
                }
                else
                {
                    MessageBox.Show(
                        unsubscribeResult.ErrorMessage ?? "取消订阅失败",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 插件设置按钮点击
        /// </summary>
        private void BtnPluginSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                ShowPluginSettings(pluginId);
            }
        }

        /// <summary>
        /// 编辑覆盖层位置按钮点击
        /// </summary>
        private void BtnEditOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                EnterOverlayEditMode(pluginId);
            }
        }

        /// <summary>
        /// 返回按钮点击
        /// </summary>
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // 保存当前配置
            SaveCurrentPluginConfig();
            
            // 切换回列表视图
            ShowPluginList();
        }

        /// <summary>
        /// 重置配置按钮点击
        /// </summary>
        private void BtnResetConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPluginId))
                return;

            var result = MessageBox.Show(
                "确定要将所有设置重置为默认值吗？",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            ResetPluginConfig(_currentPluginId);
        }

        /// <summary>
        /// 打开插件目录按钮点击
        /// </summary>
        private void BtnOpenPluginFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPluginId))
                return;

            OpenPluginFolder(_currentPluginId);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// 刷新所有内容（供外部调用）
        /// </summary>
        public void RefreshAll()
        {
            if (_currentPluginId != null && PluginSettingsPanel.Visibility == Visibility.Visible)
            {
                // 如果在设置页面，刷新设置
                ShowPluginSettings(_currentPluginId);
            }
            else
            {
                RefreshPluginList();
            }
        }

        #endregion

        #region Private Methods - View Switching

        /// <summary>
        /// 显示插件列表视图
        /// </summary>
        private void ShowPluginList()
        {
            _currentPluginId = null;
            _currentRenderer = null;
            _currentDefinition = null;
            
            PluginListPanel.Visibility = Visibility.Visible;
            PluginSettingsPanel.Visibility = Visibility.Collapsed;
            
            RefreshPluginList();
        }

        /// <summary>
        /// 显示插件设置视图
        /// </summary>
        private void ShowPluginSettings(string pluginId)
        {
            var plugin = PluginHost.Instance.GetPlugin(pluginId);
            if (plugin == null)
            {
                Debug.WriteLine($"[PluginSettings] 插件 {pluginId} 不存在");
                return;
            }

            _currentPluginId = pluginId;

            // 更新标题
            PluginSettingsTitle.Text = plugin.Manifest.Name ?? pluginId;
            PluginSettingsVersion.Text = $"v{plugin.Manifest.Version ?? "1.0.0"}";

            // 更新描述
            if (!string.IsNullOrWhiteSpace(plugin.Manifest.Description))
            {
                PluginSettingsDescription.Text = plugin.Manifest.Description;
                PluginSettingsDescription.Visibility = Visibility.Visible;
            }
            else
            {
                PluginSettingsDescription.Visibility = Visibility.Collapsed;
            }

            // 加载并渲染设置 UI
            LoadAndRenderSettingsUi(pluginId, plugin);

            // 切换视图
            PluginListPanel.Visibility = Visibility.Collapsed;
            PluginSettingsPanel.Visibility = Visibility.Visible;
        }

        #endregion

        #region Private Methods - Settings UI

        /// <summary>
        /// 加载并渲染设置 UI
        /// </summary>
        private void LoadAndRenderSettingsUi(string pluginId, PluginContext plugin)
        {
            // 清空容器
            SettingsUiContainer.Child = null;
            _currentRenderer = null;
            _currentDefinition = null;

            // 加载 settings_ui.json
            var definition = LoadSettingsUiDefinition(pluginId, plugin);
            if (definition == null || definition.Sections == null || definition.Sections.Count == 0)
            {
                NoSettingsText.Visibility = Visibility.Visible;
                BtnResetConfig.Visibility = Visibility.Collapsed;
                return;
            }

            NoSettingsText.Visibility = Visibility.Collapsed;
            BtnResetConfig.Visibility = Visibility.Visible;

            _currentDefinition = definition;

            // 获取插件配置
            var config = PluginHost.Instance.GetPluginConfig(pluginId);
            if (config == null)
            {
                Debug.WriteLine($"[PluginSettings] 无法获取插件 {pluginId} 的配置");
                return;
            }

            // 创建渲染器
            _currentRenderer = new SettingsUiRenderer(definition, config);
            
            // 订阅事件
            _currentRenderer.ValueChanged += OnSettingsValueChanged;
            _currentRenderer.ButtonAction += OnSettingsButtonAction;

            // 渲染 UI
            var settingsPanel = _currentRenderer.Render();
            SettingsUiContainer.Child = settingsPanel;

            Debug.WriteLine($"[PluginSettings] 已渲染插件 {pluginId} 的设置 UI");
        }

        /// <summary>
        /// 加载 settings_ui.json 定义
        /// </summary>
        private SettingsUiDefinition? LoadSettingsUiDefinition(string pluginId, PluginContext plugin)
        {
            // 首先尝试从插件源码目录加载
            var sourceDir = PluginRegistry.Instance.GetPluginSourceDirectory(pluginId);
            var settingsUiPath = Path.Combine(sourceDir, "settings_ui.json");

            if (File.Exists(settingsUiPath))
            {
                var definition = SettingsUiDefinition.LoadFromFile(settingsUiPath);
                if (definition != null)
                {
                    Debug.WriteLine($"[PluginSettings] 从 {settingsUiPath} 加载设置 UI 定义");
                    return definition;
                }
            }

            // 如果源码目录没有，尝试从插件目录加载（兼容旧结构）
            settingsUiPath = Path.Combine(plugin.PluginDirectory, "settings_ui.json");
            if (File.Exists(settingsUiPath))
            {
                var definition = SettingsUiDefinition.LoadFromFile(settingsUiPath);
                if (definition != null)
                {
                    Debug.WriteLine($"[PluginSettings] 从 {settingsUiPath} 加载设置 UI 定义");
                    return definition;
                }
            }

            Debug.WriteLine($"[PluginSettings] 插件 {pluginId} 没有 settings_ui.json");
            return null;
        }

        /// <summary>
        /// 设置值变更事件处理
        /// </summary>
        private void OnSettingsValueChanged(object? sender, SettingsValueChangedEventArgs e)
        {
            Debug.WriteLine($"[PluginSettings] 配置变更: {e.Key} = {e.Value}");
            
            // 自动保存配置
            if (!string.IsNullOrEmpty(_currentPluginId))
            {
                PluginHost.Instance.SavePluginConfig(_currentPluginId);
            }
        }

        /// <summary>
        /// 按钮动作事件处理
        /// </summary>
        private void OnSettingsButtonAction(object? sender, SettingsButtonActionEventArgs e)
        {
            Debug.WriteLine($"[PluginSettings] 按钮动作: {e.Action}");

            if (string.IsNullOrEmpty(_currentPluginId))
                return;

            // 处理内置动作
            switch (e.Action)
            {
                case SettingsButtonActions.EnterEditMode:
                    EnterOverlayEditMode(_currentPluginId);
                    break;

                case SettingsButtonActions.ResetConfig:
                    ResetPluginConfig(_currentPluginId);
                    break;

                case SettingsButtonActions.OpenPluginFolder:
                    OpenPluginFolder(_currentPluginId);
                    break;

                default:
                    // 自定义动作：调用插件的 onSettingAction 回调
                    var plugin = PluginHost.Instance.GetPlugin(_currentPluginId);
                    if (plugin != null && plugin.HasFunction("onSettingAction"))
                    {
                        plugin.InvokeFunction("onSettingAction", e.Action);
                    }
                    break;
            }
        }

        #endregion


        #region Private Methods - Plugin Actions

        /// <summary>
        /// 进入覆盖层编辑模式
        /// </summary>
        private void EnterOverlayEditMode(string pluginId)
        {
            var plugin = PluginHost.Instance.GetPlugin(pluginId);
            if (plugin == null)
                return;

            try
            {
                var overlay = OverlayManager.Instance.GetOverlay(pluginId);

                if (overlay == null)
                {
                    var config = PluginHost.Instance.GetPluginConfig(pluginId);

                    double x = 50, y = 50, width = 200, height = 200;

                    if (config != null)
                    {
                        x = config.Get("overlay.x", 50.0);
                        y = config.Get("overlay.y", 50.0);
                        width = config.Get("overlay.width", 200.0);
                        height = config.Get("overlay.height", 200.0);

                        var size = config.Get("overlay.size", 0.0);
                        if (size > 0 && width == 200.0)
                        {
                            width = height = size;
                        }
                    }

                    if (plugin.Manifest.DefaultConfig != null &&
                        plugin.Manifest.DefaultConfig.TryGetValue("overlay", out var defaultOverlay))
                    {
                        if (x == 50 && defaultOverlay.TryGetProperty("x", out var dxProp))
                            x = dxProp.GetDouble();
                        if (y == 50 && defaultOverlay.TryGetProperty("y", out var dyProp))
                            y = dyProp.GetDouble();
                        if (width == 200 && defaultOverlay.TryGetProperty("size", out var dsizeProp))
                            width = height = dsizeProp.GetDouble();
                    }

                    var options = new OverlayOptions
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };

                    overlay = OverlayManager.Instance.CreateOverlay(pluginId, options);
                    Debug.WriteLine($"[PluginSettings] 为插件 {pluginId} 创建 Overlay: ({x}, {y}) {width}x{height}");

                    overlay.EditModeExited += (s, args) => SaveOverlayConfig(pluginId, overlay);
                }

                overlay.Show();
                overlay.EnterEditMode();
                Debug.WriteLine($"[PluginSettings] 插件 {pluginId} 进入覆盖层编辑模式");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginSettings] 进入编辑模式失败: {ex.Message}");
                MessageBox.Show($"进入编辑模式失败: {ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存 Overlay 配置
        /// </summary>
        private void SaveOverlayConfig(string pluginId, OverlayWindow overlay)
        {
            try
            {
                var config = PluginHost.Instance.GetPluginConfig(pluginId);
                if (config == null) return;

                var rect = overlay.GetRect();

                config.Set("overlay.x", (int)rect.X);
                config.Set("overlay.y", (int)rect.Y);
                config.Set("overlay.width", (int)rect.Width);
                config.Set("overlay.height", (int)rect.Height);

                PluginHost.Instance.SavePluginConfig(pluginId);

                Debug.WriteLine($"[PluginSettings] 已保存插件 {pluginId} 的 Overlay 配置: ({rect.X}, {rect.Y}) {rect.Width}x{rect.Height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginSettings] 保存 Overlay 配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置插件配置为默认值
        /// </summary>
        private void ResetPluginConfig(string pluginId)
        {
            var plugin = PluginHost.Instance.GetPlugin(pluginId);
            if (plugin == null)
                return;

            var config = PluginHost.Instance.GetPluginConfig(pluginId);
            if (config == null)
                return;

            // 清空当前设置
            config.Settings.Clear();

            // 重新应用默认配置
            config.ApplyDefaults(plugin.Manifest.DefaultConfig);

            // 保存配置
            PluginHost.Instance.SavePluginConfig(pluginId);

            // 刷新 UI
            _currentRenderer?.RefreshValues();

            Debug.WriteLine($"[PluginSettings] 已重置插件 {pluginId} 的配置");
            MessageBox.Show("设置已重置为默认值", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 打开插件目录
        /// </summary>
        private void OpenPluginFolder(string pluginId)
        {
            var plugin = PluginHost.Instance.GetPlugin(pluginId);
            if (plugin == null)
                return;

            // 优先打开配置目录（用户可写）
            var folderPath = plugin.ConfigDirectory;
            if (!Directory.Exists(folderPath))
            {
                // 如果配置目录不存在，打开源码目录
                folderPath = plugin.PluginDirectory;
            }

            if (Directory.Exists(folderPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                    Debug.WriteLine($"[PluginSettings] 打开插件目录: {folderPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginSettings] 打开目录失败: {ex.Message}");
                    MessageBox.Show($"无法打开目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("插件目录不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 保存当前插件配置
        /// </summary>
        private void SaveCurrentPluginConfig()
        {
            if (!string.IsNullOrEmpty(_currentPluginId))
            {
                PluginHost.Instance.SavePluginConfig(_currentPluginId);
                Debug.WriteLine($"[PluginSettings] 已保存插件 {_currentPluginId} 的配置");
            }
        }

        #endregion

        #region Private Methods - Plugin List

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        private void RefreshPluginList()
        {
            var plugins = PluginHost.Instance.LoadedPlugins;

            if (plugins.Count == 0)
            {
                NoPluginsText.Visibility = Visibility.Visible;
                PluginList.Visibility = Visibility.Collapsed;
                return;
            }

            NoPluginsText.Visibility = Visibility.Collapsed;
            PluginList.Visibility = Visibility.Visible;

            var pluginViewModels = plugins.Select(p => new PluginViewModel
            {
                Id = p.PluginId,
                Name = p.Manifest.Name ?? p.PluginId,
                Version = p.Manifest.Version ?? "1.0.0",
                Description = p.Manifest.Description,
                Author = p.Manifest.Author,
                Permissions = p.Manifest.Permissions ?? new List<string>(),
                IsEnabled = p.IsEnabled,
                HasSettingsUiFile = CheckHasSettingsUi(p.PluginId, p)
            }).ToList();

            PluginList.ItemsSource = pluginViewModels;
        }

        /// <summary>
        /// 检查插件是否有 settings_ui.json
        /// </summary>
        private bool CheckHasSettingsUi(string pluginId, PluginContext plugin)
        {
            // 检查源码目录
            var sourceDir = PluginRegistry.Instance.GetPluginSourceDirectory(pluginId);
            var settingsUiPath = Path.Combine(sourceDir, "settings_ui.json");
            if (File.Exists(settingsUiPath))
                return true;

            // 检查插件目录（兼容旧结构）
            settingsUiPath = Path.Combine(plugin.PluginDirectory, "settings_ui.json");
            return File.Exists(settingsUiPath);
        }

        #endregion
    }

    #region ViewModel

    /// <summary>
    /// 插件视图模型
    /// </summary>
    public class PluginViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public List<string> Permissions { get; set; } = new();
        public bool HasSettingsUiFile { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                }
            }
        }

        public Visibility HasDescription => string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility HasPermissions => Permissions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasOverlayPermission => Permissions.Contains("overlay") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility HasSettingsUi => HasSettingsUiFile ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    #endregion
}
