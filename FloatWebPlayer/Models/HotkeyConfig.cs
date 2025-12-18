using System.Collections.Generic;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 快捷键配置根模型
    /// </summary>
    public class HotkeyConfig
    {
        /// <summary>
        /// 所有配置 Profile 列表
        /// </summary>
        public List<HotkeyProfile> Profiles { get; set; } = new();

        /// <summary>
        /// 当前激活的 Profile 名称
        /// </summary>
        public string ActiveProfileName { get; set; } = "Default";

        /// <summary>
        /// 是否启用进程自动切换 Profile
        /// </summary>
        public bool AutoSwitchProfile { get; set; } = false;

        /// <summary>
        /// 获取当前激活的 Profile
        /// </summary>
        /// <returns>当前 Profile，未找到则返回 null</returns>
        public HotkeyProfile? GetActiveProfile()
        {
            foreach (var profile in Profiles)
            {
                if (profile.Name == ActiveProfileName)
                    return profile;
            }

            // 回退到第一个 Profile
            return Profiles.Count > 0 ? Profiles[0] : null;
        }

        /// <summary>
        /// 根据进程名查找应激活的 Profile（用于自动切换）
        /// </summary>
        /// <param name="processName">前台进程名</param>
        /// <returns>匹配的 Profile，未找到返回当前激活的 Profile</returns>
        public HotkeyProfile? FindProfileForProcess(string? processName)
        {
            if (!AutoSwitchProfile || string.IsNullOrEmpty(processName))
                return GetActiveProfile();

            foreach (var profile in Profiles)
            {
                if (profile.ShouldActivateFor(processName))
                    return profile;
            }

            // 未匹配则使用当前激活的 Profile
            return GetActiveProfile();
        }

        /// <summary>
        /// 创建默认配置（兼容现有快捷键）
        /// </summary>
        /// <returns>默认配置</returns>
        public static HotkeyConfig CreateDefault()
        {
            var config = new HotkeyConfig
            {
                ActiveProfileName = "Default",
                AutoSwitchProfile = false,
                Profiles = new List<HotkeyProfile>
                {
                    new HotkeyProfile
                    {
                        Name = "Default",
                        ActivationProcesses = null,
                        Bindings = new List<HotkeyBinding>
                        {
                            // 5 键 - 视频倒退
                            new HotkeyBinding { Key = 0x35, Modifiers = ModifierKeys.None, Action = "SeekBackward" },
                            // 6 键 - 视频前进
                            new HotkeyBinding { Key = 0x36, Modifiers = ModifierKeys.None, Action = "SeekForward" },
                            // ` 波浪键 - 播放/暂停
                            new HotkeyBinding { Key = 0xC0, Modifiers = ModifierKeys.None, Action = "TogglePlay" },
                            // 7 键 - 降低透明度
                            new HotkeyBinding { Key = 0x37, Modifiers = ModifierKeys.None, Action = "DecreaseOpacity" },
                            // 8 键 - 增加透明度
                            new HotkeyBinding { Key = 0x38, Modifiers = ModifierKeys.None, Action = "IncreaseOpacity" },
                            // 0 键 - 切换鼠标穿透
                            new HotkeyBinding { Key = 0x30, Modifiers = ModifierKeys.None, Action = "ToggleClickThrough" },
                            // Alt+Enter - 切换最大化
                            new HotkeyBinding { Key = 0x0D, Modifiers = ModifierKeys.Alt, Action = "ToggleMaximize" }
                        }
                    }
                }
            };

            return config;
        }
    }
}
