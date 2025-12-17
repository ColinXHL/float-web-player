using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Services;

namespace FloatWebPlayer.Views
{
    /// <summary>
    /// 插件选择对话框 - 从已安装插件中选择添加到 Profile
    /// </summary>
    public partial class PluginSelectorDialog : AnimatedWindow
    {
        private readonly string _profileId;
        private readonly List<PluginSelectorItem> _items;

        public PluginSelectorDialog(List<InstalledPluginInfo> availablePlugins, string profileId)
        {
            InitializeComponent();
            _profileId = profileId;

            // 转换为选择项
            _items = availablePlugins.Select(p => new PluginSelectorItem
            {
                Id = p.Id,
                Name = p.Name,
                Version = p.Version,
                Description = p.Description,
                IsSelected = false
            }).ToList();

            // 监听选择变化
            foreach (var item in _items)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            PluginList.ItemsSource = _items;
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PluginSelectorItem.IsSelected))
            {
                UpdateConfirmButton();
            }
        }

        private void UpdateConfirmButton()
        {
            var selectedCount = _items.Count(i => i.IsSelected);
            BtnConfirm.IsEnabled = selectedCount > 0;
            BtnConfirm.Content = selectedCount > 0 
                ? $"添加选中的 {selectedCount} 个插件" 
                : "添加选中的插件";
        }

        private void PluginItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pluginId)
            {
                var item = _items.FirstOrDefault(i => i.Id == pluginId);
                if (item != null)
                {
                    item.IsSelected = !item.IsSelected;
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            var selectedPlugins = _items.Where(i => i.IsSelected).Select(i => i.Id).ToList();
            
            if (selectedPlugins.Count == 0)
            {
                MessageBox.Show("请至少选择一个插件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 添加到 Profile
            var addedCount = PluginAssociationManager.Instance.AddPluginsToProfile(selectedPlugins, _profileId);
            
            if (addedCount > 0)
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("添加失败，插件可能已存在于此 Profile 中", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
            }
            
            Close();
        }
    }

    /// <summary>
    /// 插件选择项
    /// </summary>
    public class PluginSelectorItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string? Description { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public Visibility HasDescriptionVisibility => HasDescription ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
