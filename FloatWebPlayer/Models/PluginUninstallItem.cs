using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 插件卸载项数据模型
    /// 用于在 PluginUninstallDialog 中显示可选择卸载的插件
    /// </summary>
    public class PluginUninstallItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 是否选中要卸载（默认选中）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
