using System;
using System.Collections.Generic;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 事件系统 API
    /// 提供应用程序事件监听功能
    /// 需要 "events" 权限
    /// </summary>
    public class EventApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly Dictionary<string, List<Action<object>>> _listeners;

        #endregion

        #region Event Names

        /// <summary>播放状态变化事件</summary>
        public const string PlayStateChanged = "playStateChanged";
        /// <summary>播放时间更新事件</summary>
        public const string TimeUpdate = "timeUpdate";
        /// <summary>透明度变化事件</summary>
        public const string OpacityChanged = "opacityChanged";
        /// <summary>穿透模式变化事件</summary>
        public const string ClickThroughChanged = "clickThroughChanged";
        /// <summary>URL 变化事件</summary>
        public const string UrlChanged = "urlChanged";
        /// <summary>Profile 切换事件</summary>
        public const string ProfileChanged = "profileChanged";

        /// <summary>
        /// 所有支持的事件名称
        /// </summary>
        public static readonly string[] SupportedEvents = new[]
        {
            PlayStateChanged, TimeUpdate, OpacityChanged, 
            ClickThroughChanged, UrlChanged, ProfileChanged
        };

        #endregion

        #region Constructor

        /// <summary>
        /// 创建事件 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public EventApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _listeners = new Dictionary<string, List<Action<object>>>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 注册事件监听器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">回调函数</param>
        public void On(string eventName, Action<object> callback)
        {
            if (string.IsNullOrWhiteSpace(eventName) || callback == null)
                return;

            if (!_listeners.TryGetValue(eventName, out var list))
            {
                list = new List<Action<object>>();
                _listeners[eventName] = list;
            }

            if (!list.Contains(callback))
            {
                list.Add(callback);
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: registered listener for '{eventName}'");
            }
        }

        /// <summary>
        /// 取消事件监听
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="callback">回调函数（为 null 时移除该事件的所有监听器）</param>
        public void Off(string eventName, Action<object>? callback = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            if (!_listeners.TryGetValue(eventName, out var list))
                return;

            if (callback == null)
            {
                // 移除该事件的所有监听器
                list.Clear();
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed all listeners for '{eventName}'");
            }
            else
            {
                // 移除指定的监听器
                list.Remove(callback);
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: removed listener for '{eventName}'");
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 触发事件（供主程序调用）
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        public void Emit(string eventName, object data)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            if (!_listeners.TryGetValue(eventName, out var list) || list.Count == 0)
                return;

            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", $"EventApi: emitting '{eventName}' to {list.Count} listeners");

            // 复制列表以避免在迭代时修改
            var callbacks = list.ToArray();
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(data);
                }
                catch (Exception ex)
                {
                    // 捕获异常，记录日志，继续执行其他回调
                    Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", 
                        $"EventApi: callback for '{eventName}' threw exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 清除所有监听器（插件卸载时调用）
        /// </summary>
        internal void ClearAllListeners()
        {
            _listeners.Clear();
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "EventApi: cleared all listeners");
        }

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            ClearAllListeners();
        }

        /// <summary>
        /// 获取指定事件的监听器数量
        /// </summary>
        internal int GetListenerCount(string eventName)
        {
            if (_listeners.TryGetValue(eventName, out var list))
                return list.Count;
            return 0;
        }

        #endregion
    }
}
