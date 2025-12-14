using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件设置页面
    /// </summary>
    public partial class PluginSettingsPage : UserControl
    {
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
                // 获取插件名称用于显示
                var plugin = PluginHost.Instance.GetPlugin(pluginId);
                var pluginName = plugin?.Manifest.Name ?? pluginId;

                // 显示确认对话框
                var result = MessageBox.Show(
                    $"确定要取消订阅插件 \"{pluginName}\" 吗？\n\n此操作将停止插件运行并删除插件目录，无法撤销。",
                    "确认取消订阅",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // 调用 PluginHost.UnsubscribePlugin
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
                    Debug.WriteLine($"[PluginSettings] 取消订阅插件 {pluginId} 失败: {unsubscribeResult.ErrorMessage}");
                }
            }
        }

        /// <summary>
        /// 编辑覆盖层位置按钮点击
        /// </summary>
        private void BtnEditOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pluginId)
            {
                var plugin = PluginHost.Instance.GetPlugin(pluginId);
                if (plugin != null)
                {
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

        #endregion

        #region Private Methods

        /// <summary>
        /// 刷新所有内容（供外部调用）
        /// </summary>
        public void RefreshAll()
        {
            RefreshPluginList();
        }

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
                IsEnabled = p.IsEnabled
            }).ToList();

            PluginList.ItemsSource = pluginViewModels;
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

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    #endregion
}
