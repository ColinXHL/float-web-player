using System;
using System.Windows;
using System.Windows.Interop;

namespace FloatWebPlayer.Helpers
{
    /// <summary>
    /// 输入法切换助手
    /// 用于在快捷键输入时自动切换到英文模式
    /// </summary>
    public static class ImeHelper
    {
        /// <summary>
        /// 保存的 IME 状态信息
        /// </summary>
        public struct ImeState
        {
            /// <summary>
            /// 窗口句柄
            /// </summary>
            public IntPtr Hwnd;

            /// <summary>
            /// IME 上下文句柄
            /// </summary>
            public IntPtr HiMC;

            /// <summary>
            /// 之前的 IME 开启状态
            /// </summary>
            public bool WasOpen;

            /// <summary>
            /// 状态是否有效
            /// </summary>
            public bool IsValid;
        }

        /// <summary>
        /// 切换到英文输入模式
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <returns>之前的 IME 状态（用于恢复）</returns>
        public static ImeState SwitchToEnglish(Window window)
        {
            var state = new ImeState { IsValid = false };

            try
            {
                if (window == null)
                    return state;

                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return state;

                state.Hwnd = hwnd;

                // 获取 IME 上下文
                var hIMC = Win32Helper.GetImeContext(hwnd);
                if (hIMC == IntPtr.Zero)
                    return state;

                state.HiMC = hIMC;

                // 保存当前 IME 状态
                state.WasOpen = Win32Helper.GetImeOpenStatus(hIMC);
                state.IsValid = true;

                // 关闭 IME（切换到英文模式）
                if (state.WasOpen)
                {
                    Win32Helper.SetImeOpenStatus(hIMC, false);
                }

                // 释放 IME 上下文
                Win32Helper.ReleaseImeContext(hwnd, hIMC);
            }
            catch
            {
                // 静默忽略所有错误（需求 2.4）
            }

            return state;
        }

        /// <summary>
        /// 切换到英文输入模式（使用窗口句柄）
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <returns>之前的 IME 状态（用于恢复）</returns>
        public static ImeState SwitchToEnglish(IntPtr hwnd)
        {
            var state = new ImeState { IsValid = false };

            try
            {
                if (hwnd == IntPtr.Zero)
                    return state;

                state.Hwnd = hwnd;

                // 获取 IME 上下文
                var hIMC = Win32Helper.GetImeContext(hwnd);
                if (hIMC == IntPtr.Zero)
                    return state;

                state.HiMC = hIMC;

                // 保存当前 IME 状态
                state.WasOpen = Win32Helper.GetImeOpenStatus(hIMC);
                state.IsValid = true;

                // 关闭 IME（切换到英文模式）
                if (state.WasOpen)
                {
                    Win32Helper.SetImeOpenStatus(hIMC, false);
                }

                // 释放 IME 上下文
                Win32Helper.ReleaseImeContext(hwnd, hIMC);
            }
            catch
            {
                // 静默忽略所有错误（需求 2.4）
            }

            return state;
        }

        /// <summary>
        /// 恢复之前的输入法状态
        /// </summary>
        /// <param name="state">之前保存的 IME 状态</param>
        public static void RestoreImeState(ImeState state)
        {
            try
            {
                if (!state.IsValid || state.Hwnd == IntPtr.Zero)
                    return;

                // 如果之前 IME 是开启的，则恢复
                if (state.WasOpen)
                {
                    // 重新获取 IME 上下文
                    var hIMC = Win32Helper.GetImeContext(state.Hwnd);
                    if (hIMC == IntPtr.Zero)
                        return;

                    // 恢复 IME 状态
                    Win32Helper.SetImeOpenStatus(hIMC, true);

                    // 释放 IME 上下文
                    Win32Helper.ReleaseImeContext(state.Hwnd, hIMC);
                }
            }
            catch
            {
                // 静默忽略所有错误（需求 2.4）
            }
        }

        /// <summary>
        /// 恢复之前的输入法状态（使用窗口和之前状态）
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="previousState">之前保存的 IME 状态</param>
        public static void RestoreImeState(Window window, ImeState previousState)
        {
            RestoreImeState(previousState);
        }
    }
}
