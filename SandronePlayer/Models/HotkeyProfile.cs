using System;
using System.Collections.Generic;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 快捷键配置 Profile
    /// 支持多配置场景，可根据进程自动切换
    /// </summary>
    public class HotkeyProfile
    {
        /// <summary>
        /// Profile 名称
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// 自动激活此 Profile 的进程列表
        /// null 表示不自动激活（需手动选择）
        /// </summary>
        public List<string>? ActivationProcesses { get; set; }

        /// <summary>
        /// 此 Profile 下的快捷键绑定列表
        /// </summary>
        public List<HotkeyBinding> Bindings { get; set; } = new();

        /// <summary>
        /// 检查当前进程是否应自动激活此 Profile
        /// </summary>
        /// <param name="processName">当前前台进程名</param>
        /// <returns>是否匹配</returns>
        public bool ShouldActivateFor(string? processName)
        {
            if (ActivationProcesses == null || ActivationProcesses.Count == 0)
                return false;

            if (string.IsNullOrEmpty(processName))
                return false;

            foreach (var proc in ActivationProcesses)
            {
                if (string.Equals(proc, processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 查找匹配的快捷键绑定
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <param name="modifiers">修饰键状态</param>
        /// <param name="processName">当前前台进程名</param>
        /// <returns>匹配的绑定，未找到返回 null</returns>
        public HotkeyBinding? FindMatchingBinding(uint vkCode, ModifierKeys modifiers, string? processName)
        {
            foreach (var binding in Bindings)
            {
                if (!binding.IsEnabled)
                    continue;

                if (binding.MatchesKey(vkCode, modifiers) && binding.MatchesProcess(processName))
                    return binding;
            }

            return null;
        }
    }
}
