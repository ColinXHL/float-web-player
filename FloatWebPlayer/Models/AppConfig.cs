using System.Collections.Generic;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 应用配置模型
    /// 用于 JSON 序列化存储用户设置（Phase 15）
    /// </summary>
    public class AppConfig
    {
        #region Video Control

        /// <summary>
        /// 快进/倒退秒数
        /// </summary>
        public int SeekSeconds { get; set; } = AppConstants.DefaultSeekSeconds;

        #endregion

        #region Opacity

        /// <summary>
        /// 默认透明度
        /// </summary>
        public double DefaultOpacity { get; set; } = AppConstants.MaxOpacity;

        #endregion

        #region Hotkeys

        // 快进键
        public uint HotkeySeekForward { get; set; } = Helpers.Win32Helper.VK_6;
        public ModifierKeys HotkeySeekForwardMod { get; set; } = ModifierKeys.None;

        // 倒退键
        public uint HotkeySeekBackward { get; set; } = Helpers.Win32Helper.VK_5;
        public ModifierKeys HotkeySeekBackwardMod { get; set; } = ModifierKeys.None;

        // 播放/暂停键
        public uint HotkeyTogglePlay { get; set; } = Helpers.Win32Helper.VK_OEM_3;
        public ModifierKeys HotkeyTogglePlayMod { get; set; } = ModifierKeys.None;

        // 增加透明度键
        public uint HotkeyIncreaseOpacity { get; set; } = Helpers.Win32Helper.VK_8;
        public ModifierKeys HotkeyIncreaseOpacityMod { get; set; } = ModifierKeys.None;

        // 降低透明度键
        public uint HotkeyDecreaseOpacity { get; set; } = Helpers.Win32Helper.VK_7;
        public ModifierKeys HotkeyDecreaseOpacityMod { get; set; } = ModifierKeys.None;

        // 切换鼠标穿透键
        public uint HotkeyToggleClickThrough { get; set; } = Helpers.Win32Helper.VK_0;
        public ModifierKeys HotkeyToggleClickThroughMod { get; set; } = ModifierKeys.None;

        // 切换最大化键 (Alt+Enter)
        public uint HotkeyToggleMaximize { get; set; } = 0x0D; // VK_RETURN (Enter)
        public ModifierKeys HotkeyToggleMaximizeMod { get; set; } = ModifierKeys.Alt;

        #endregion

        #region Profile

        /// <summary>
        /// 当前选择的 Profile ID
        /// </summary>
        public string CurrentProfileId { get; set; } = AppConstants.DefaultProfileId;

        #endregion

        #region Window Behavior

        /// <summary>
        /// 是否启用边缘吸附
        /// </summary>
        public bool EnableEdgeSnap { get; set; } = true;

        /// <summary>
        /// 边缘吸附阈值（像素）
        /// </summary>
        public int SnapThreshold { get; set; } = AppConstants.SnapThreshold;

        #endregion

        #region Conversion Methods

        /// <summary>
        /// 将 AppConfig 转换为 HotkeyConfig
        /// </summary>
        /// <returns>包含所有快捷键绑定的 HotkeyConfig</returns>
        public HotkeyConfig ToHotkeyConfig()
        {
            return new HotkeyConfig
            {
                ActiveProfileName = "Default",
                AutoSwitchProfile = false,
                Profiles = new List<HotkeyProfile>
                {
                    new HotkeyProfile
                    {
                        Name = "Default",
                        ActivationProcesses = null,
                        Bindings = CreateHotkeyBindings()
                    }
                }
            };
        }

        /// <summary>
        /// 创建快捷键绑定列表
        /// </summary>
        /// <returns>包含所有 7 个快捷键绑定的列表</returns>
        private List<HotkeyBinding> CreateHotkeyBindings()
        {
            return new List<HotkeyBinding>
            {
                new HotkeyBinding { Key = HotkeySeekBackward, Modifiers = HotkeySeekBackwardMod, Action = "SeekBackward" },
                new HotkeyBinding { Key = HotkeySeekForward, Modifiers = HotkeySeekForwardMod, Action = "SeekForward" },
                new HotkeyBinding { Key = HotkeyTogglePlay, Modifiers = HotkeyTogglePlayMod, Action = "TogglePlay" },
                new HotkeyBinding { Key = HotkeyDecreaseOpacity, Modifiers = HotkeyDecreaseOpacityMod, Action = "DecreaseOpacity" },
                new HotkeyBinding { Key = HotkeyIncreaseOpacity, Modifiers = HotkeyIncreaseOpacityMod, Action = "IncreaseOpacity" },
                new HotkeyBinding { Key = HotkeyToggleClickThrough, Modifiers = HotkeyToggleClickThroughMod, Action = "ToggleClickThrough" },
                new HotkeyBinding { Key = HotkeyToggleMaximize, Modifiers = HotkeyToggleMaximizeMod, Action = "ToggleMaximize" }
            };
        }

        #endregion
    }
}
