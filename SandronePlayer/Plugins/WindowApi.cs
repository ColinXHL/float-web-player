using System;
using SandronePlayer.Helpers;
using SandronePlayer.Views;

namespace SandronePlayer.Plugins
{
    /// <summary>
    /// 窗口控制 API
    /// 提供播放器窗口状态控制功能
    /// 需要 "window" 权限
    /// </summary>
    public class WindowApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly Func<PlayerWindow?> _getWindow;
        private EventApi? _eventApi;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建窗口 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public WindowApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _getWindow = () => null;
        }

        /// <summary>
        /// 创建窗口 API 实例（带窗口引用）
        /// </summary>
        /// <param name="context">插件上下文</param>
        /// <param name="getWindow">获取 PlayerWindow 的委托</param>
        public WindowApi(PluginContext context, Func<PlayerWindow?> getWindow)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _getWindow = getWindow ?? throw new ArgumentNullException(nameof(getWindow));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 设置 EventApi 引用（用于触发事件）
        /// </summary>
        internal void SetEventApi(EventApi? eventApi)
        {
            _eventApi = eventApi;
        }

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            // WindowApi 没有事件监听器需要清理
            // 清空 EventApi 引用
            _eventApi = null;
            
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "WindowApi: cleaned up");
        }

        #endregion

        #region Opacity Control

        /// <summary>
        /// 设置窗口透明度
        /// </summary>
        /// <param name="opacity">透明度（0.2 到 1.0）</param>
        public void SetOpacity(double opacity)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.SetOpacity: PlayerWindow not available");
                return;
            }

            // 钳制透明度到有效范围
            var clampedOpacity = Math.Clamp(opacity, AppConstants.MinOpacity, AppConstants.MaxOpacity);
            
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", 
                $"WindowApi.SetOpacity({opacity}) -> clamped to {clampedOpacity}");

            // 在 UI 线程执行
            window.Dispatcher.Invoke(() =>
            {
                // 获取当前透明度用于比较
                var oldOpacity = GetOpacityInternal(window);
                
                // 使用 Win32Helper 设置透明度
                Win32Helper.SetWindowOpacity(window, clampedOpacity);
                
                // 触发事件（如果透明度确实改变了）
                if (Math.Abs(oldOpacity - clampedOpacity) > 0.001 && _eventApi != null)
                {
                    _eventApi.Emit(EventApi.OpacityChanged, new { opacity = clampedOpacity });
                }
            });
        }

        /// <summary>
        /// 获取当前窗口透明度
        /// </summary>
        /// <returns>透明度（0.2 到 1.0）</returns>
        public double GetOpacity()
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.GetOpacity: PlayerWindow not available");
                return 1.0;
            }

            return window.Dispatcher.Invoke(() => GetOpacityInternal(window));
        }

        /// <summary>
        /// 内部方法：获取透明度（必须在 UI 线程调用）
        /// </summary>
        private double GetOpacityInternal(PlayerWindow window)
        {
            // 使用 OpacityPercent 属性获取当前透明度
            return window.OpacityPercent / 100.0;
        }

        #endregion

        #region Click-Through Control

        /// <summary>
        /// 设置鼠标穿透模式
        /// </summary>
        /// <param name="enabled">是否启用穿透</param>
        public void SetClickThrough(bool enabled)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.SetClickThrough: PlayerWindow not available");
                return;
            }

            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", 
                $"WindowApi.SetClickThrough({enabled})");

            window.Dispatcher.Invoke(() =>
            {
                var currentState = window.IsClickThrough;
                
                // 只有状态不同时才切换
                if (currentState != enabled)
                {
                    window.ToggleClickThrough();
                    
                    // 触发事件
                    _eventApi?.Emit(EventApi.ClickThroughChanged, new { enabled = enabled });
                }
            });
        }

        /// <summary>
        /// 获取当前穿透模式状态
        /// </summary>
        /// <returns>是否启用穿透</returns>
        public bool IsClickThrough()
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.IsClickThrough: PlayerWindow not available");
                return false;
            }

            return window.Dispatcher.Invoke(() => window.IsClickThrough);
        }

        #endregion

        #region Topmost Control

        /// <summary>
        /// 设置窗口置顶状态
        /// </summary>
        /// <param name="topmost">是否置顶</param>
        public void SetTopmost(bool topmost)
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.SetTopmost: PlayerWindow not available");
                return;
            }

            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", 
                $"WindowApi.SetTopmost({topmost})");

            window.Dispatcher.Invoke(() =>
            {
                window.Topmost = topmost;
            });
        }

        /// <summary>
        /// 获取当前置顶状态
        /// </summary>
        /// <returns>是否置顶</returns>
        public bool IsTopmost()
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.IsTopmost: PlayerWindow not available");
                return true; // 默认返回 true，因为播放器通常是置顶的
            }

            return window.Dispatcher.Invoke(() => window.Topmost);
        }

        #endregion

        #region Window Bounds

        /// <summary>
        /// 获取窗口位置和大小
        /// </summary>
        /// <returns>包含 x, y, width, height 的对象</returns>
        public object GetBounds()
        {
            var window = _getWindow();
            if (window == null)
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", 
                    "WindowApi.GetBounds: PlayerWindow not available");
                return new { x = 0.0, y = 0.0, width = 800.0, height = 600.0 };
            }

            return window.Dispatcher.Invoke(() => new
            {
                x = window.Left,
                y = window.Top,
                width = window.Width,
                height = window.Height
            });
        }

        #endregion
    }
}
