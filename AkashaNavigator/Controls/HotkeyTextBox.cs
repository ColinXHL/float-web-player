using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using ConfigModifierKeys = AkashaNavigator.Models.Config.ModifierKeys;
using ConfigInputType = AkashaNavigator.Models.Config.InputType;

namespace AkashaNavigator.Controls
{
/// <summary>
/// 快捷键输入框自定义控件
/// 封装快捷键编辑逻辑，符合 MVVM 模式
/// </summary>
public class HotkeyTextBox : System.Windows.Controls.TextBox
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", nameof(HotkeyTextBox));

    #region Dependency Properties

    /// <summary>
    /// 虚拟键码依赖属性
    /// </summary>
    public static readonly DependencyProperty HotkeyValueProperty =
        DependencyProperty.Register(
            nameof(HotkeyValue),
            typeof(uint),
            typeof(HotkeyTextBox),
            new PropertyMetadata(0u, OnHotkeyValuePropertyChanged));

    /// <summary>
    /// 修饰键依赖属性
    /// </summary>
    public static readonly DependencyProperty ModifiersProperty =
        DependencyProperty.Register(
            nameof(Modifiers),
            typeof(ConfigModifierKeys),
            typeof(HotkeyTextBox),
            new PropertyMetadata(ConfigModifierKeys.None, OnModifiersPropertyChanged));

    /// <summary>
    /// 输入类型依赖属性
    /// </summary>
    public static readonly DependencyProperty InputTypeProperty =
        DependencyProperty.Register(
            nameof(InputType),
            typeof(ConfigInputType),
            typeof(HotkeyTextBox),
            new PropertyMetadata(ConfigInputType.Keyboard, OnInputTypePropertyChanged));

    #endregion

    #region Properties

    /// <summary>
    /// 虚拟键码（双向绑定）
    /// </summary>
    public uint HotkeyValue
    {
        get => (uint)GetValue(HotkeyValueProperty);
        set => SetValue(HotkeyValueProperty, value);
    }

    /// <summary>
    /// 修饰键（双向绑定）
    /// </summary>
    public ConfigModifierKeys Modifiers
    {
        get => (ConfigModifierKeys)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    /// <summary>
    /// 输入类型（双向绑定）
    /// </summary>
    public ConfigInputType InputType
    {
        get => (ConfigInputType)GetValue(InputTypeProperty);
        set => SetValue(InputTypeProperty, value);
    }

    #endregion

    #region Fields

    private string _originalText = string.Empty;
    private ImeHelper.ImeState _savedImeState;
    private bool _isProcessingKey;
    private bool _recording;
    private System.Windows.Threading.DispatcherTimer? _lostFocusTimer;

    // 全局低级键盘钩子（WH_KEYBOARD_LL）：在系统输入流源头捕获 Alt 组合键，
    // 绕开 WPF 的 AccessKey/系统菜单对 Alt 组合键的拦截
    private IntPtr _keyboardHook;
    private static Win32Helper.LowLevelKeyboardProc? _keyboardHookProc; // 防 GC

    // 系统保留 VK 码（Win32Helper 未定义）
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_F10 = 0x79;
    private const uint LLKHF_ALTDOWN = 0x20;

    #endregion

    #region Constructor

    static HotkeyTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyTextBox),
            new FrameworkPropertyMetadata(typeof(HotkeyTextBox)));
    }

    public HotkeyTextBox()
    {
        TextAlignment = TextAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsReadOnly = false;  // 改为 false，通过 PreviewTextInput 阻止文本输入

        UpdateDisplayText();

        PreviewKeyDown += OnPreviewKeyDown;
        KeyDown += OnKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewTextInput += OnPreviewTextInput;  // 阻止文本输入
        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
    }

    /// <summary>
    /// 阻止文本输入，只允许快捷键编辑
    /// </summary>
    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;  // 阻止所有文本输入
    }

    #endregion

    #region Event Handlers

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        _originalText = Text;
        Text = "按下新快捷键...";

        _recording = true;
        LostFocusTimer.Stop();

        // Alt 组合键会被 WPF 的 AccessKey/系统菜单吞掉，需在系统输入流源头拦截
        AttachKeyboardHook();
        // 录键期间移除系统菜单，防止 Alt 激活菜单抢走键盘焦点
        SetSystemMenuEnabled(false);

        // 切换到英文输入模式
        _savedImeState = ImeHelper.SwitchToEnglish(Window.GetWindow(this));
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        UpdateDisplayText();
        ImeHelper.RestoreImeState(_savedImeState);

        // 延迟 1s 结束录键：Alt 按下时系统可能临时转移焦点，给组合键留录入时间
        LostFocusTimer.Start();
    }

    /// <summary>
    /// 录键状态延迟清除计时器（单实例，Tick 只订阅一次）
    /// </summary>
    private System.Windows.Threading.DispatcherTimer LostFocusTimer
    {
        get
        {
            if (_lostFocusTimer == null)
            {
                _lostFocusTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _lostFocusTimer.Tick += (_, _) =>
                {
                    _lostFocusTimer.Stop();
                    _recording = false;
                    DetachKeyboardHook();
                    SetSystemMenuEnabled(true);
                };
            }
            return _lostFocusTimer;
        }
    }

    /// <summary>
    /// 挂全局低级键盘钩子：Alt 组合键在系统输入流源头被捕获并录入
    /// </summary>
    private void AttachKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
            return;
        _keyboardHookProc = KeyboardHookCallback;
        _keyboardHook = Win32Helper.SetKeyboardHook(_keyboardHookProc);
    }

    /// <summary>
    /// 摘除全局低级键盘钩子
    /// </summary>
    private void DetachKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
            return;
        Win32Helper.RemoveKeyboardHook(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
        _keyboardHookProc = null;
    }

    /// <summary>
    /// 全局键盘钩子回调：捕获 Alt 组合键并录入，吞掉按键使 WPF/系统收不到
    /// </summary>
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recording)
        {
            int msg = wParam.ToInt32();
            if (msg == Win32Helper.WM_KEYDOWN || msg == Win32Helper.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<Win32Helper.KBDLLHOOKSTRUCT>(lParam);
                uint vk = data.vkCode;

                if ((data.flags & LLKHF_ALTDOWN) != 0 && IsRecordableCombo(vk))
                {
                    var modifiers = ConfigModifierKeys.Alt;
                    if (Win32Helper.IsKeyPressed(Win32Helper.VK_CONTROL))
                        modifiers |= ConfigModifierKeys.Ctrl;
                    if (Win32Helper.IsKeyPressed(Win32Helper.VK_SHIFT))
                        modifiers |= ConfigModifierKeys.Shift;

                    _isProcessingKey = true;
                    try
                    {
                        SetHotkey(vk, modifiers, ConfigInputType.Keyboard);
                    }
                    finally
                    {
                        _isProcessingKey = false;
                    }
                    return (IntPtr)1; // 吞掉按键
                }
            }
        }
        return Win32Helper.CallNextHook(_keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Alt 组合键中可录入的键（排除修饰键与系统保留组合）
    /// </summary>
    private static bool IsRecordableCombo(uint vk) =>
        vk != Win32Helper.VK_MENU &&
        vk != VK_TAB && vk != VK_ESCAPE && vk != VK_F4 && vk != VK_F10;

    /// <summary>
    /// 启用/禁用窗口系统菜单（录键期间禁用，防止 Alt 激活菜单抢焦点）
    /// </summary>
    private void SetSystemMenuEnabled(bool enabled)
    {
        var window = Window.GetWindow(this);
        if (window == null)
            return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var style = Win32Helper.GetWindowStyle(hwnd);
        var newStyle = enabled ? style | Win32Helper.WS_SYSMENU : style & ~Win32Helper.WS_SYSMENU;
        if (newStyle != style)
            Win32Helper.SetWindowStyle(hwnd, newStyle);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // ESC 键备用处理
        if (e.Key == Key.Escape && !e.Handled)
        {
            e.Handled = true;
            ClearHotkey();
            MoveFocusToWindow();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isProcessingKey)
            return;

        _isProcessingKey = true;

        try
        {
            Key targetKey = e.Key;
            bool isSystemKey = e.Key == Key.System;

            if (isSystemKey)
            {
                // Alt 组合键可能以两种形态到达：
                // 1) 普通键（IsSystem=False，日志实证：真实场景组合键走这里）
                // 2) 系统键（IsSystem=True，SystemKey=实际键）
                // 两种都要处理，此处恢复 SystemKey 解析
                targetKey = e.SystemKey;

                // 排除系统级快捷键（Alt+Tab 等）
                if (targetKey == Key.Tab)
                {
                    return;  // 不处理，让系统处理
                }
            }

            // ESC 键：清空快捷键绑定
            if (targetKey == Key.Escape)
            {
                e.Handled = true;
                ClearHotkey();
                MoveFocusToWindow();
                return;
            }

            // 忽略修饰键本身
            if (IsModifierKey(targetKey))
            {
                e.Handled = true;
                return;
            }

            // 获取虚拟键码
            var vkCode = (uint)KeyInterop.VirtualKeyFromKey(targetKey);

            // 获取当前修饰键状态
            var modifiers = GetModifierKeys(isSystemKey);

            e.Handled = true;
            SetHotkey(vkCode, modifiers, ConfigInputType.Keyboard);
        }
        finally
        {
            _isProcessingKey = false;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isProcessingKey)
            return;

        uint mouseButton = e.ChangedButton switch
        {
            MouseButton.XButton1 => MouseButtonCodes.XButton1,
            MouseButton.XButton2 => MouseButtonCodes.XButton2,
            _ => 0
        };

        if (mouseButton == 0)
            return;

        _isProcessingKey = true;

        try
        {
            e.Handled = true;
            var modifiers = GetModifierKeys(isSystemKey: false);
            SetHotkey(mouseButton, modifiers, ConfigInputType.Mouse);
        }
        finally
        {
            _isProcessingKey = false;
        }
    }

    #endregion

    #region Private Methods

    private static void OnHotkeyValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            // 总是更新显示文本
            textBox.UpdateDisplayText();
        }
    }

    private static void OnModifiersPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            // 总是更新显示文本
            textBox.UpdateDisplayText();
        }
    }

    private static void OnInputTypePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox)
        {
            textBox.UpdateDisplayText();
        }
    }

    /// <summary>
    /// 判断是否为修饰键
    /// </summary>
    private static bool IsModifierKey(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl ||
               key == Key.LeftAlt || key == Key.RightAlt ||
               key == Key.LeftShift || key == Key.RightShift ||
               key == Key.LWin || key == Key.RWin;
    }

    /// <summary>
    /// 获取当前修饰键状态
    /// （Alt 组合键由全局钩子处理，此处服务于 Ctrl/Shift 组合与鼠标按键）
    /// </summary>
    private static ConfigModifierKeys GetModifierKeys(bool isSystemKey)
    {
        var modifiers = ConfigModifierKeys.None;
        if (Win32Helper.IsKeyPressed(Win32Helper.VK_CONTROL))
            modifiers |= ConfigModifierKeys.Ctrl;
        if (isSystemKey || Win32Helper.IsKeyPressed(Win32Helper.VK_MENU))
            modifiers |= ConfigModifierKeys.Alt;
        if (Win32Helper.IsKeyPressed(Win32Helper.VK_SHIFT))
            modifiers |= ConfigModifierKeys.Shift;
        return modifiers;
    }

    /// <summary>
    /// 更新显示文本
    /// </summary>
    private void UpdateDisplayText()
    {
        Text = HotkeyValue == 0 ? string.Empty : Win32Helper.GetHotkeyDisplayName(HotkeyValue, Modifiers);
    }

    /// <summary>
    /// 清空快捷键
    /// </summary>
    private void ClearHotkey()
    {
        HotkeyValue = 0;
        Modifiers = ConfigModifierKeys.None;
        InputType = ConfigInputType.Keyboard;
    }

    private void SetHotkey(uint key, ConfigModifierKeys modifiers, ConfigInputType inputType)
    {
        HotkeyValue = key;
        Modifiers = modifiers;
        InputType = inputType;
        UpdateDisplayText();
        MoveFocusToWindow();
    }

    /// <summary>
    /// 将焦点移回窗口
    /// </summary>
    private void MoveFocusToWindow()
    {
        // 延迟执行，确保 LostFocus 事件先触发
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // 强制清除焦点
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
            Keyboard.ClearFocus();

            // 将焦点设置到窗口，而不是控件本身
            var window = Window.GetWindow(this);
            if (window != null)
            {
                // 设置焦点到窗口，但不让任何子元素获得焦点
                window.Focusable = true;
                window.Focus();
                Keyboard.Focus(null);
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    #endregion
}
}
