using System;
using System.Collections.Generic;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 动作分发器
    /// 将字符串 Action 标识符映射到实际的执行处理器
    /// 支持后续扩展自定义脚本等
    /// </summary>
    public class ActionDispatcher
    {
        #region Built-in Action Names

        /// <summary>视频倒退</summary>
        public const string ActionSeekBackward = "SeekBackward";
        /// <summary>视频前进</summary>
        public const string ActionSeekForward = "SeekForward";
        /// <summary>播放/暂停</summary>
        public const string ActionTogglePlay = "TogglePlay";
        /// <summary>降低透明度</summary>
        public const string ActionDecreaseOpacity = "DecreaseOpacity";
        /// <summary>增加透明度</summary>
        public const string ActionIncreaseOpacity = "IncreaseOpacity";
        /// <summary>切换鼠标穿透</summary>
        public const string ActionToggleClickThrough = "ToggleClickThrough";
        /// <summary>切换最大化</summary>
        public const string ActionToggleMaximize = "ToggleMaximize";

        #endregion

        #region Fields

        private readonly Dictionary<string, Action> _handlers = new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Events (兼容现有 HotkeyService 事件模式)

        /// <summary>视频倒退事件</summary>
        public event EventHandler? SeekBackward;
        /// <summary>视频前进事件</summary>
        public event EventHandler? SeekForward;
        /// <summary>播放/暂停切换事件</summary>
        public event EventHandler? TogglePlay;
        /// <summary>降低透明度事件</summary>
        public event EventHandler? DecreaseOpacity;
        /// <summary>增加透明度事件</summary>
        public event EventHandler? IncreaseOpacity;
        /// <summary>切换鼠标穿透模式事件</summary>
        public event EventHandler? ToggleClickThrough;
        /// <summary>切换最大化事件</summary>
        public event EventHandler? ToggleMaximize;

        #endregion

        #region Constructor

        public ActionDispatcher()
        {
            // 注册内置动作
            RegisterBuiltinActions();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 注册自定义动作处理器
        /// </summary>
        /// <param name="actionName">动作名称</param>
        /// <param name="handler">处理器</param>
        public void RegisterAction(string actionName, Action handler)
        {
            _handlers[actionName] = handler;
        }

        /// <summary>
        /// 取消注册动作
        /// </summary>
        /// <param name="actionName">动作名称</param>
        public void UnregisterAction(string actionName)
        {
            _handlers.Remove(actionName);
        }

        /// <summary>
        /// 分发执行动作
        /// </summary>
        /// <param name="actionName">动作名称</param>
        /// <returns>是否成功执行</returns>
        public bool Dispatch(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return false;

            // 检查是否是自定义脚本动作（预留扩展点）
            if (actionName.StartsWith("Script:", StringComparison.OrdinalIgnoreCase))
            {
                return DispatchScript(actionName.Substring(7));
            }

            // 查找已注册的处理器
            if (_handlers.TryGetValue(actionName, out var handler))
            {
                handler.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查动作是否已注册
        /// </summary>
        /// <param name="actionName">动作名称</param>
        /// <returns>是否已注册</returns>
        public bool IsActionRegistered(string actionName)
        {
            return _handlers.ContainsKey(actionName);
        }

        /// <summary>
        /// 获取所有已注册的动作名称
        /// </summary>
        /// <returns>动作名称列表</returns>
        public IEnumerable<string> GetRegisteredActions()
        {
            return _handlers.Keys;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 注册内置动作，通过事件触发保持与现有代码兼容
        /// </summary>
        private void RegisterBuiltinActions()
        {
            RegisterAction(ActionSeekBackward, () => SeekBackward?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionSeekForward, () => SeekForward?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionTogglePlay, () => TogglePlay?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionDecreaseOpacity, () => DecreaseOpacity?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionIncreaseOpacity, () => IncreaseOpacity?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionToggleClickThrough, () => ToggleClickThrough?.Invoke(this, EventArgs.Empty));
            RegisterAction(ActionToggleMaximize, () => ToggleMaximize?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>
        /// 分发脚本动作（预留扩展点）
        /// </summary>
        /// <param name="scriptName">脚本名称</param>
        /// <returns>是否成功执行</returns>
        private bool DispatchScript(string scriptName)
        {
            // TODO: 后续实现自定义脚本执行
            // 可以支持 Lua、JavaScript 等脚本引擎
            System.Diagnostics.Debug.WriteLine($"Script action not implemented: {scriptName}");
            return false;
        }

        #endregion
    }
}
