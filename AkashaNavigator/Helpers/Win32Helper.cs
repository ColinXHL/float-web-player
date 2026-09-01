using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AkashaNavigator.Models.Config;
using Serilog;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// Win32 API 封装，用于窗口操作
/// </summary>
public static class Win32Helper
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "Win32Helper");

#region Win32 API - Window

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// 跨平台 GetWindowLongPtr（兼容 32/64 位）
    /// </summary>
    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
    }

    /// <summary>
    /// 跨平台 SetWindowLongPtr（兼容 32/64 位）
    /// </summary>
    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                                : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
    }

    // 兼容旧代码的包装方法
    private static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        return (int)GetWindowLongPtr(hWnd, nIndex);
    }

    private static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        return (int)SetWindowLongPtr(hWnd, nIndex, new IntPtr(dwNewLong));
    }

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy,
                                            uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClipCursor(out RECT lpRect);

    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hmonitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEX lpmi);

#endregion

#region Win32 API - Keyboard Hook

    /// <summary>
    /// 低级键盘钩子回调委托
    /// </summary>
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

#endregion

#region Win32 API - IME(Input Method Editor)

    /// <summary>
    /// 获取窗口的输入法上下文句柄
    /// </summary>
    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    /// <summary>
    /// 释放输入法上下文句柄
    /// </summary>
    [DllImport("imm32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    /// <summary>
    /// 设置输入法开启/关闭状态
    /// </summary>
    [DllImport("imm32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmSetOpenStatus(IntPtr hIMC, [MarshalAs(UnmanagedType.Bool)] bool fOpen);

    /// <summary>
    /// 获取输入法开启/关闭状态
    /// </summary>
    [DllImport("imm32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmGetOpenStatus(IntPtr hIMC);

#endregion

#region Win32 Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// 键盘钩子结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>
    /// 鼠标光标信息结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    /// <summary>
    /// 显示器信息结构体（GetMonitorInfoW 使用）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

#endregion

#region Constants - Window

    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    // 窗口位置消息
    public const int WM_MOVING = 0x0216;
    public const int WM_SIZING = 0x0214;
    public const int WM_ENTERSIZEMOVE = 0x0231;
    public const int WM_EXITSIZEMOVE = 0x0232;

    // 调整大小方向常量
    private const int SC_SIZE_HTLEFT = 0xF001;
    private const int SC_SIZE_HTRIGHT = 0xF002;
    private const int SC_SIZE_HTTOP = 0xF003;
    private const int SC_SIZE_HTTOPLEFT = 0xF004;
    private const int SC_SIZE_HTTOPRIGHT = 0xF005;
    private const int SC_SIZE_HTBOTTOM = 0xF006;
    private const int SC_SIZE_HTBOTTOMLEFT = 0xF007;
    private const int SC_SIZE_HTBOTTOMRIGHT = 0xF008;

    // 扩展窗口样式常量
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // SetWindowPos 标志
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;

    // SetLayeredWindowAttributes 标志
    private const uint LWA_ALPHA = 0x02;

    // 系统度量常量
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    // 多显示器常量
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    public const uint MONITORINFOF_PRIMARY = 0x00000001;

    // 虚拟键码
    public const int VK_LBUTTON = 0x01;

    // 光标信息标志
    public const int CURSOR_SHOWING = 0x00000001;

#endregion

    #region Monitor Methods

    /// <summary>
    /// 获取包含指定窗口的显示器句柄
    /// 如果窗口跨越多个显示器，返回包含窗口最大面积的显示器
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>显示器句柄，失败返回 IntPtr.Zero</returns>
    public static IntPtr GetMonitorFromWindow(IntPtr hwnd)
    {
        return MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    }

    /// <summary>
    /// 获取显示器的详细信息（工作区、完整区域、设备名称、是否为主显示器）
    /// </summary>
    /// <param name="hMonitor">显示器句柄</param>
    /// <returns>显示器快照信息，失败返回 null</returns>
    public static MonitorInfo? GetMonitorInfo(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero)
            return null;

        var info = new MONITORINFOEX();
        info.cbSize = Marshal.SizeOf<MONITORINFOEX>();

        if (!GetMonitorInfoW(hMonitor, ref info))
            return null;

        return new MonitorInfo
        {
            Handle = hMonitor,
            DpiScale = GetDpiScaleForMonitor(hMonitor),
            MonitorRect = info.rcMonitor,
            WorkAreaRect = info.rcWork,
            IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
            DeviceName = info.szDevice ?? string.Empty
        };
    }

    /// <summary>
    /// 获取包含指定窗口的显示器信息
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>显示器快照信息，失败返回 null</returns>
    public static MonitorInfo? GetMonitorInfoFromWindow(IntPtr hwnd)
    {
        IntPtr hMonitor = GetMonitorFromWindow(hwnd);
        return GetMonitorInfo(hMonitor);
    }

    /// <summary>
    /// 枚举所有活动显示器
    /// 使用 Win32 EnumDisplayMonitors API 遍历所有连接的显示器
    /// </summary>
    /// <returns>显示器信息列表</returns>
    public static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdcMonitor, lprcMonitor, lParam) =>
        {
            var info = GetMonitorInfo(hMonitor);
            if (info != null)
            {
                monitors.Add(info);
            }
            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    /// <summary>
    /// 显示器枚举回调委托
    /// </summary>
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

#endregion

#region Constants - Keyboard Hook

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    // 快捷键虚拟键码
    public const uint VK_0 = 0x30;
    public const uint VK_5 = 0x35;
    public const uint VK_6 = 0x36;
    public const uint VK_7 = 0x37;
    public const uint VK_8 = 0x38;
    public const uint VK_OEM_3 = 0xC0; // ` 波浪键

    // 修饰键虚拟键码
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt 键

#endregion

#region Key Name Mapping

    /// <summary>
    /// 获取虚拟键码的显示名称
    /// </summary>
    public static string GetKeyName(uint vkCode)
    {
        return vkCode switch {                     // 鼠标侧键
                              0x05 => "鼠标侧键1", // XButton1
                              0x06 => "鼠标侧键2", // XButton2

                              // 数字键
                              >= 0x30 and <=
                                  0x39 => ((char)vkCode).ToString(),

                              // 字母键
                              >= 0x41 and <=
                                  0x5A => ((char)vkCode).ToString(),

                              // 功能键
                              >= 0x70 and <=
                                  0x7B => $"F{vkCode - 0x70 + 1}",

                              // 数字小键盘
                              >= 0x60 and <=
                                  0x69 => $"Num{vkCode - 0x60}",

                              // 特殊键
                              0x08 => "Backspace",
                              0x09 => "Tab",
                              0x0D => "Enter",
                              0x1B => "Esc",
                              0x20 => "Space",
                              0x21 => "PageUp",
                              0x22 => "PageDown",
                              0x23 => "End",
                              0x24 => "Home",
                              0x25 => "←",
                              0x26 => "↑",
                              0x27 => "→",
                              0x28 => "↓",
                              0x2D => "Insert",
                              0x2E => "Delete",

                              // OEM 键
                              0xBA => ";",
                              0xBB => "=",
                              0xBC => ",",
                              0xBD => "-",
                              0xBE => ".",
                              0xBF => "/",
                              0xC0 => "`", // OEM_3 波浪键
                              0xDB => "[",
                              0xDC => "\\",
                              0xDD => "]",
                              0xDE => "'",

                              // 数字小键盘运算符
                              0x6A => "*",
                              0x6B => "+",
                              0x6D => "-",
                              0x6E => ".",
                              0x6F => "/",

                              _ => $"0x{vkCode:X2}"
        };
    }

    /// <summary>
    /// 获取组合键的显示名称（包含修饰键）
    /// </summary>
    public static string GetHotkeyDisplayName(uint vkCode, ModifierKeys modifiers)
    {
        // 空绑定（Key=0）返回空字符串
        if (vkCode == 0)
            return string.Empty;

        var parts = new System.Collections.Generic.List<string>();

        if (modifiers.HasFlag(ModifierKeys.Ctrl))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");

        parts.Add(GetKeyName(vkCode));

        return string.Join("+", parts);
    }

#endregion

#region Resize Direction Enum

    /// <summary>
    /// 调整大小方向
    /// </summary>
    public enum ResizeDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        TopLeft = 4,
        TopRight = 5,
        Bottom = 6,
        BottomLeft = 7,
        BottomRight = 8
    }

#endregion

#region Public Methods

    /// <summary>
    /// 检测鼠标位置对应的调整大小方向
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="mousePosition">鼠标相对窗口的位置</param>
    /// <param name="borderThickness">边框厚度</param>
    /// <returns>调整方向</returns>
    public static ResizeDirection GetResizeDirection(Window window, System.Windows.Point mousePosition,
                                                     int borderThickness = 6)
    {
        double width = window.ActualWidth;
        double height = window.ActualHeight;

        bool left = mousePosition.X < borderThickness;
        bool right = mousePosition.X > width - borderThickness;
        bool top = mousePosition.Y < borderThickness;
        bool bottom = mousePosition.Y > height - borderThickness;

        if (top && left)
            return ResizeDirection.TopLeft;
        if (top && right)
            return ResizeDirection.TopRight;
        if (bottom && left)
            return ResizeDirection.BottomLeft;
        if (bottom && right)
            return ResizeDirection.BottomRight;
        if (left)
            return ResizeDirection.Left;
        if (right)
            return ResizeDirection.Right;
        if (top)
            return ResizeDirection.Top;
        if (bottom)
            return ResizeDirection.Bottom;

        return ResizeDirection.None;
    }

    /// <summary>
    /// 开始调整窗口大小
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="direction">调整方向</param>
    public static void StartResize(Window window, ResizeDirection direction)
    {
        if (direction == ResizeDirection.None)
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        int scSize = direction switch { ResizeDirection.Left => SC_SIZE_HTLEFT,
                                        ResizeDirection.Right => SC_SIZE_HTRIGHT,
                                        ResizeDirection.Top => SC_SIZE_HTTOP,
                                        ResizeDirection.TopLeft => SC_SIZE_HTTOPLEFT,
                                        ResizeDirection.TopRight => SC_SIZE_HTTOPRIGHT,
                                        ResizeDirection.Bottom => SC_SIZE_HTBOTTOM,
                                        ResizeDirection.BottomLeft => SC_SIZE_HTBOTTOMLEFT,
                                        ResizeDirection.BottomRight => SC_SIZE_HTBOTTOMRIGHT,
                                        _ => 0 };

        if (scSize != 0)
        {
            ReleaseCapture();
            SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)scSize, IntPtr.Zero);
        }
    }

    /// <summary>
    /// 开始移动窗口（模拟标题栏拖动）
    /// </summary>
    /// <param name="window">目标窗口</param>
    public static void StartMove(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        ReleaseCapture();
        SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    /// <summary>
    /// 设置窗口鼠标穿透模式
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="enable">是否启用穿透</param>
    public static void SetClickThrough(Window window, bool enable)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            Logger.Warning("SetClickThrough: hwnd is Zero!");
            return;
        }

        int exStyleBefore = GetWindowLong(hwnd, GWL_EXSTYLE);
        Logger.Debug("SetClickThrough: enable={Enable}, exStyleBefore=0x{ExStyleBefore:X8}", enable, exStyleBefore);

        int exStyle = exStyleBefore;
        if (enable)
        {
            // 添加 WS_EX_TRANSPARENT 和 WS_EX_LAYERED
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        }
        else
        {
            // 移除 WS_EX_TRANSPARENT（保留 WS_EX_LAYERED 用于透明度）
            exStyle &= ~WS_EX_TRANSPARENT;
        }

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        int exStyleAfter = GetWindowLong(hwnd, GWL_EXSTYLE);
        Logger.Debug("SetClickThrough: exStyleAfter=0x{ExStyleAfter:X8}, expected=0x{Expected:X8}, success={Success}",
                     exStyleAfter, exStyle, exStyleAfter == exStyle);
    }

    /// <summary>
    /// 设置窗口透明度（0.0 - 1.0）
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="opacity">透明度值（0.0 完全透明，1.0 完全不透明）</param>
    public static void SetWindowOpacity(Window window, double opacity)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // 确保窗口有 WS_EX_LAYERED 样式
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) == 0)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        }

        // 设置透明度（0-255）
        byte alpha = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 255);
        SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
    }

    /// <summary>
    /// 获取窗口是否处于鼠标穿透模式
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>是否穿透</returns>
    public static bool IsClickThrough(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TRANSPARENT) != 0;
    }

    /// <summary>
    /// 检查鼠标是否在窗口区域内
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>鼠标是否在窗口内</returns>
    public static bool IsCursorInWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        if (!GetCursorPos(out POINT pt))
            return false;
        if (!GetWindowRect(hwnd, out RECT rect))
            return false;

        // 使用 Win32 API 获取的窗口坐标（物理像素）进行比较
        return pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom;
    }

#endregion

#region Cursor &Screen Methods

    /// <summary>
    /// 获取当前鼠标位置（物理像素）
    /// </summary>
    /// <param name="point">输出鼠标位置</param>
    /// <returns>是否成功</returns>
    public static bool GetCursorPosition(out POINT point)
    {
        return GetCursorPos(out point);
    }

    /// <summary>
    /// 获取窗口矩形（物理像素）
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="rect">输出矩形</param>
    /// <returns>是否成功</returns>
    public static bool GetWindowRectangle(IntPtr hwnd, out RECT rect)
    {
        return GetWindowRect(hwnd, out rect);
    }

    /// <summary>
    /// 以物理像素设置窗口位置和大小
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="x">左边界（物理像素）</param>
    /// <param name="y">上边界（物理像素）</param>
    /// <param name="width">宽度（物理像素）</param>
    /// <param name="height">高度（物理像素）</param>
    /// <returns>是否成功</returns>
    public static bool SetWindowRectangle(IntPtr hwnd, int x, int y, int width, int height)
    {
        return SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE | SWP_NOZORDER);
    }

    /// <summary>
    /// 获取窗口当前 DPI 缩放比例。
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>DPI 缩放比例，获取失败时返回 1.0</returns>
    public static double GetDpiScaleForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return 1.0;
        }

        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    /// <summary>
    /// 获取指定显示器的有效 DPI 缩放比例。
    /// </summary>
    public static double GetDpiScaleForMonitor(IntPtr hmonitor)
    {
        if (hmonitor == IntPtr.Zero)
        {
            return 1.0;
        }

        try
        {
            const int effectiveDpi = 0;
            var result = GetDpiForMonitor(hmonitor, effectiveDpi, out var dpiX, out _);
            return result == 0 && dpiX > 0 ? dpiX / 96.0 : 1.0;
        }
        catch (DllNotFoundException)
        {
            return 1.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0;
        }
    }

    /// <summary>
    /// 获取屏幕尺寸
    /// </summary>
    /// <param name="index">SM_CXSCREEN 或 SM_CYSCREEN</param>
    /// <returns>像素值</returns>
    public static int GetScreenMetrics(int index)
    {
        return GetSystemMetrics(index);
    }

    /// <summary>
    /// 检查指定按键是否被按下
    /// </summary>
    /// <param name="vKey">虚拟键码</param>
    /// <returns>是否按下</returns>
    public static bool IsKeyPressed(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    /// <summary>
    /// 检查系统鼠标光标是否可见
    /// 用于检测游戏是否隐藏了鼠标（如 FPS 模式）
    /// </summary>
    /// <returns>鼠标是否可见</returns>
    public static bool IsCursorVisible()
    {
        var cursorInfo = new CURSORINFO();
        cursorInfo.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

        if (GetCursorInfo(ref cursorInfo))
        {
            return (cursorInfo.flags & CURSOR_SHOWING) != 0;
        }

        // 如果获取失败，默认返回 true（假设可见）
        return true;
    }

    /// <summary>
    /// 检测光标是否被限制在中心区域（游戏模式）
    /// </summary>
    /// <remarks>
    /// 游戏在隐藏鼠标时通常会使用 ClipCursor 将光标限制在窗口中心的小区域。
    /// 当打开游戏 UI（如地图、菜单）时，会取消裁剪，光标可以自由移动。
    /// </remarks>
    /// <returns>true = 游戏模式（光标被限制），false = UI 模式（光标自由）</returns>
    public static bool IsCursorClippedToCenter()
    {
        if (!GetClipCursor(out RECT clip))
            return false;  // API 调用失败，假设 UI 模式

        int clipWidth = clip.Right - clip.Left;
        int clipHeight = clip.Bottom - clip.Top;

        // 获取主显示器尺寸
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        if (screenWidth <= 0 || screenHeight <= 0)
            return false;

        // 如果裁剪区域小于屏幕尺寸的 95%，说明游戏限制了光标（游戏模式）
        bool isClipped =
            clipWidth < screenWidth * 0.95 ||
            clipHeight < screenHeight * 0.95;

        return isClipped;
    }

#endregion

#region Window Style Methods

    /// <summary>
    /// 获取窗口扩展样式
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>扩展样式值</returns>
    public static int GetWindowExStyle(IntPtr hwnd)
    {
        return GetWindowLong(hwnd, GWL_EXSTYLE);
    }

    /// <summary>
    /// 设置窗口扩展样式
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="exStyle">扩展样式值</param>
    public static void SetWindowExStyle(IntPtr hwnd, int exStyle)
    {
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 设置窗口为工具窗口样式（从 Alt+Tab 隐藏）
    /// </summary>
    /// <param name="window">目标窗口</param>
    public static void SetToolWindowStyle(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// 设置窗口不激活样式（点击或操作时不会获得焦点）
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="enable">是否启用不激活样式</param>
    public static void SetNoActivateStyle(Window window, bool enable)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (enable)
        {
            exStyle |= WS_EX_NOACTIVATE;
        }
        else
        {
            exStyle &= ~WS_EX_NOACTIVATE;
        }
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 检查窗口是否设置了不激活样式
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>是否设置了不激活样式</returns>
    public static bool HasNoActivateStyle(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_NOACTIVATE) != 0;
    }

#endregion

#region Keyboard Hook Methods

    /// <summary>
    /// 设置低级键盘钩子
    /// </summary>
    /// <param name="proc">回调函数</param>
    /// <returns>钩子句柄</returns>
    public static IntPtr SetKeyboardHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    /// <summary>
    /// 移除键盘钩子
    /// </summary>
    /// <param name="hookId">钩子句柄</param>
    /// <returns>是否成功</returns>
    public static bool RemoveKeyboardHook(IntPtr hookId)
    {
        return UnhookWindowsHookEx(hookId);
    }

    /// <summary>
    /// 调用下一个钩子
    /// </summary>
    /// <param name="hookId">钩子句柄</param>
    /// <param name="nCode">代码</param>
    /// <param name="wParam">参数1</param>
    /// <param name="lParam">参数2</param>
    /// <returns>结果</returns>
    public static IntPtr CallNextHook(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam)
    {
        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

#endregion

#region Edge Snapping Methods

    /// <summary>
    /// 将窗口矩形吸附到屏幕边缘（简单版，无滞后）
    /// </summary>
    /// <param name="rect">窗口矩形（物理像素）</param>
    /// <param name="workArea">工作区矩形（物理像素）</param>
    /// <param name="threshold">吸附阈值（物理像素）</param>
    public static void SnapRectToEdges(ref RECT rect, RECT workArea, int threshold)
    {
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        // 左边缘吸附
        if (Math.Abs(rect.Left - workArea.Left) <= threshold)
        {
            rect.Left = workArea.Left;
            rect.Right = rect.Left + width;
        }
        // 右边缘吸附
        else if (Math.Abs(rect.Right - workArea.Right) <= threshold)
        {
            rect.Right = workArea.Right;
            rect.Left = rect.Right - width;
        }

        // 上边缘吸附
        if (Math.Abs(rect.Top - workArea.Top) <= threshold)
        {
            rect.Top = workArea.Top;
            rect.Bottom = rect.Top + height;
        }
        // 下边缘吸附
        else if (Math.Abs(rect.Bottom - workArea.Bottom) <= threshold)
        {
            rect.Bottom = workArea.Bottom;
            rect.Top = rect.Bottom - height;
        }
    }

    /// <summary>
    /// 调整大小时将窗口边缘吸附到屏幕边缘
    /// </summary>
    /// <param name="rect">窗口矩形（物理像素）</param>
    /// <param name="workArea">工作区矩形（物理像素）</param>
    /// <param name="threshold">吸附阈值（物理像素）</param>
    /// <param name="sizingEdge">调整大小的边缘 (WMSZ_* 常量)</param>
    /// <returns>是否进行了吸附</returns>
    public static bool SnapSizingEdge(ref RECT rect, RECT workArea, int threshold, int sizingEdge)
    {
        bool snapped = false;

        // 根据调整方向吸附对应边缘
        // WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_BOTTOM = 6
        // WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8

        // 左边缘
        if (sizingEdge == 1 || sizingEdge == 4 || sizingEdge == 7)
        {
            if (Math.Abs(rect.Left - workArea.Left) < threshold)
            {
                rect.Left = workArea.Left;
                snapped = true;
            }
        }

        // 右边缘
        if (sizingEdge == 2 || sizingEdge == 5 || sizingEdge == 8)
        {
            if (Math.Abs(rect.Right - workArea.Right) < threshold)
            {
                rect.Right = workArea.Right;
                snapped = true;
            }
        }

        // 上边缘
        if (sizingEdge == 3 || sizingEdge == 4 || sizingEdge == 5)
        {
            if (Math.Abs(rect.Top - workArea.Top) < threshold)
            {
                rect.Top = workArea.Top;
                snapped = true;
            }
        }

        // 下边缘
        if (sizingEdge == 6 || sizingEdge == 7 || sizingEdge == 8)
        {
            if (Math.Abs(rect.Bottom - workArea.Bottom) < threshold)
            {
                rect.Bottom = workArea.Bottom;
                snapped = true;
            }
        }

        return snapped;
    }

    /// <summary>
    /// 将 WPF Rect 转换为物理像素 RECT
    /// </summary>
    /// <param name="rect">WPF 逻辑像素矩形</param>
    /// <param name="dpiScale">DPI 缩放比例</param>
    /// <returns>物理像素 RECT</returns>
    public static RECT ToPhysicalRect(System.Windows.Rect rect, double dpiScale)
    {
        return new RECT { Left = (int)(rect.Left * dpiScale), Top = (int)(rect.Top * dpiScale),
                          Right = (int)(rect.Right * dpiScale), Bottom = (int)(rect.Bottom * dpiScale) };
    }

#endregion

#region Process Methods

    /// <summary>
    /// 获取前台窗口的进程名（不含路径和扩展名）
    /// </summary>
    /// <returns>进程名，失败返回 null</returns>
    public static string? GetForegroundWindowProcessName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前按下的修饰键状态
    /// </summary>
    /// <returns>修饰键标志组合</returns>
    public static ModifierKeys GetCurrentModifiers()
    {
        var modifiers = ModifierKeys.None;

        if (IsKeyPressed(VK_CONTROL))
            modifiers |= ModifierKeys.Ctrl;
        if (IsKeyPressed(VK_MENU))
            modifiers |= ModifierKeys.Alt;
        if (IsKeyPressed(VK_SHIFT))
            modifiers |= ModifierKeys.Shift;

        return modifiers;
    }

#endregion

#region IME Methods

    /// <summary>
    /// 获取窗口的 IME 上下文
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <returns>IME 上下文句柄，失败返回 IntPtr.Zero</returns>
    public static IntPtr GetImeContext(IntPtr hwnd)
    {
        return ImmGetContext(hwnd);
    }

    /// <summary>
    /// 释放 IME 上下文
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="hIMC">IME 上下文句柄</param>
    /// <returns>是否成功</returns>
    public static bool ReleaseImeContext(IntPtr hwnd, IntPtr hIMC)
    {
        return ImmReleaseContext(hwnd, hIMC);
    }

    /// <summary>
    /// 设置 IME 开启/关闭状态
    /// </summary>
    /// <param name="hIMC">IME 上下文句柄</param>
    /// <param name="open">true 开启，false 关闭（切换到英文）</param>
    /// <returns>是否成功</returns>
    public static bool SetImeOpenStatus(IntPtr hIMC, bool open)
    {
        return ImmSetOpenStatus(hIMC, open);
    }

    /// <summary>
    /// 获取 IME 开启/关闭状态
    /// </summary>
    /// <param name="hIMC">IME 上下文句柄</param>
    /// <returns>true 表示 IME 开启（中文模式），false 表示关闭（英文模式）</returns>
    public static bool GetImeOpenStatus(IntPtr hIMC)
    {
        return ImmGetOpenStatus(hIMC);
    }

#endregion
}
}
