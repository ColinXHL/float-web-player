using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 全局快捷键服务，使用低级键盘钩子实现
    /// 支持配置驱动的快捷键绑定、组合键检测、进程过滤
    /// 按键不会被拦截，既能触发快捷键功能，又能正常输入
    /// 当焦点在输入控件时不触发快捷键
    /// </summary>
    public class HotkeyService : IDisposable
    {
        #region Fields

        private IntPtr _hookId = IntPtr.Zero;
        private Win32Helper.LowLevelKeyboardProc? _hookProc;
        private bool _isStarted;
        private bool _disposed;

        private HotkeyConfig _config;
        private readonly ActionDispatcher _dispatcher;

        #endregion

        #region Events (兼容旧 API，代理到 ActionDispatcher)

        /// <summary>视频倒退事件</summary>
        public event EventHandler? SeekBackward
        {
            add => _dispatcher.SeekBackward += value;
            remove => _dispatcher.SeekBackward -= value;
        }

        /// <summary>视频前进事件</summary>
        public event EventHandler? SeekForward
        {
            add => _dispatcher.SeekForward += value;
            remove => _dispatcher.SeekForward -= value;
        }

        /// <summary>播放/暂停切换事件</summary>
        public event EventHandler? TogglePlay
        {
            add => _dispatcher.TogglePlay += value;
            remove => _dispatcher.TogglePlay -= value;
        }

        /// <summary>降低透明度事件</summary>
        public event EventHandler? DecreaseOpacity
        {
            add => _dispatcher.DecreaseOpacity += value;
            remove => _dispatcher.DecreaseOpacity -= value;
        }

        /// <summary>增加透明度事件</summary>
        public event EventHandler? IncreaseOpacity
        {
            add => _dispatcher.IncreaseOpacity += value;
            remove => _dispatcher.IncreaseOpacity -= value;
        }

        /// <summary>切换鼠标穿透模式事件</summary>
        public event EventHandler? ToggleClickThrough
        {
            add => _dispatcher.ToggleClickThrough += value;
            remove => _dispatcher.ToggleClickThrough -= value;
        }

        /// <summary>切换最大化事件</summary>
        public event EventHandler? ToggleMaximize
        {
            add => _dispatcher.ToggleMaximize += value;
            remove => _dispatcher.ToggleMaximize -= value;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 创建快捷键服务（使用默认配置）
        /// </summary>
        public HotkeyService() : this(HotkeyConfig.CreateDefault(), new ActionDispatcher())
        {
        }

        /// <summary>
        /// 创建快捷键服务
        /// </summary>
        /// <param name="config">快捷键配置</param>
        /// <param name="dispatcher">动作分发器</param>
        public HotkeyService(HotkeyConfig config, ActionDispatcher dispatcher)
        {
            _config = config;
            _dispatcher = dispatcher;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 启动快捷键服务
        /// </summary>
        public void Start()
        {
            if (_isStarted) return;

            _hookProc = HookCallback;
            _hookId = Win32Helper.SetKeyboardHook(_hookProc);
            _isStarted = true;
        }

        /// <summary>
        /// 停止快捷键服务
        /// </summary>
        public void Stop()
        {
            if (!_isStarted) return;

            if (_hookId != IntPtr.Zero)
            {
                Win32Helper.RemoveKeyboardHook(_hookId);
                _hookId = IntPtr.Zero;
            }

            _hookProc = null;
            _isStarted = false;
        }

        /// <summary>
        /// 更新快捷键配置
        /// </summary>
        /// <param name="config">新配置</param>
        public void UpdateConfig(HotkeyConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        /// <returns>当前快捷键配置</returns>
        public HotkeyConfig GetConfig() => _config;

        /// <summary>
        /// 获取动作分发器（用于注册自定义动作）
        /// </summary>
        /// <returns>动作分发器</returns>
        public ActionDispatcher GetDispatcher() => _dispatcher;

        #endregion

        #region Private Methods

        /// <summary>
        /// 键盘钩子回调
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)Win32Helper.WM_KEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<Win32Helper.KBDLLHOOKSTRUCT>(lParam);
                var vkCode = hookStruct.vkCode;

                // 获取当前修饰键状态
                var modifiers = Win32Helper.GetCurrentModifiers();

                // 获取前台进程名
                var processName = Win32Helper.GetForegroundWindowProcessName();

                // 在 UI 线程上处理
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    // 输入模式检测：焦点在输入控件时不触发快捷键
                    if (IsInputMode())
                        return;

                    // 查找匹配的快捷键绑定
                    var profile = _config.FindProfileForProcess(processName);
                    if (profile == null)
                        return;

                    var binding = profile.FindMatchingBinding(vkCode, modifiers, processName);
                    if (binding != null)
                    {
                        _dispatcher.Dispatch(binding.Action);
                    }
                });
            }

            // 关键：调用 CallNextHookEx 让按键继续传递，不拦截
            return Win32Helper.CallNextHook(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 检测当前是否处于输入模式（焦点在输入控件上）
        /// </summary>
        /// <returns>是否处于输入模式</returns>
        private static bool IsInputMode()
        {
            var focusedElement = Keyboard.FocusedElement;
            
            // 检查焦点元素是否为输入控件
            return focusedElement is TextBox 
                || focusedElement is PasswordBox 
                || focusedElement is RichTextBox
                || focusedElement is TextBoxBase
                || focusedElement is ComboBox { IsEditable: true };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Stop();
            }

            _disposed = true;
        }

        ~HotkeyService()
        {
            Dispose(false);
        }

        #endregion
    }
}
