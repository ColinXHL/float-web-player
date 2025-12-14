using System;
using System.Windows;
using FloatWebPlayer.Services;
using FloatWebPlayer.Views;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 覆盖层 API
    /// 提供插件创建和控制覆盖层窗口的功能
    /// 需要 "overlay" 权限
    /// 所有坐标使用逻辑像素（与 DPI 无关）
    /// </summary>
    public class OverlayApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly ConfigApi _configApi;
        private OverlayWindow? _overlay;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建覆盖层 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        /// <param name="configApi">配置 API（用于保存位置/大小）</param>
        public OverlayApi(PluginContext context, ConfigApi configApi)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configApi = configApi ?? throw new ArgumentNullException(nameof(configApi));
        }

        #endregion

        #region Position & Size Methods

        /// <summary>
        /// 设置覆盖层窗口位置（逻辑像素）
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        public void SetPosition(int x, int y)
        {
            EnsureOverlay();
            InvokeOnUI(() => _overlay?.SetPosition(x, y));
        }

        /// <summary>
        /// 设置覆盖层窗口大小（逻辑像素）
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public void SetSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            EnsureOverlay();
            InvokeOnUI(() => _overlay?.SetSize(width, height));
        }


        /// <summary>
        /// 获取覆盖层窗口的位置和大小（逻辑像素）
        /// </summary>
        /// <returns>包含 x, y, width, height 的对象</returns>
        public object GetRect()
        {
            EnsureOverlay();
            
            double x = 0, y = 0, width = 200, height = 200;
            
            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    var rect = _overlay.GetRect();
                    x = rect.X;
                    y = rect.Y;
                    width = rect.Width;
                    height = rect.Height;
                }
            });

            return new { x, y, width, height };
        }

        #endregion

        #region Visibility Methods

        /// <summary>
        /// 显示覆盖层窗口
        /// </summary>
        public void Show()
        {
            EnsureOverlay();
            InvokeOnUI(() => _overlay?.Show());
        }

        /// <summary>
        /// 隐藏覆盖层窗口
        /// </summary>
        public void Hide()
        {
            InvokeOnUI(() => _overlay?.Hide());
        }

        #endregion

        #region Direction Marker Methods

        /// <summary>
        /// 显示方向标记
        /// </summary>
        /// <param name="direction">方向：north/northeast/east/southeast/south/southwest/west/northwest</param>
        /// <param name="duration">显示时长（毫秒），0 表示常驻</param>
        public void ShowMarker(string direction, int duration = 0)
        {
            Services.LogService.Instance.Debug("OverlayApi", $"ShowMarker called: direction={direction}, duration={duration}");

            if (string.IsNullOrWhiteSpace(direction))
            {
                Services.LogService.Instance.Warn("OverlayApi", "ShowMarker: direction is null or whitespace");
                return;
            }

            var dir = ParseDirection(direction);
            if (dir == null)
            {
                Services.LogService.Instance.Warn("OverlayApi", $"ShowMarker: invalid direction '{direction}'");
                return;
            }

            Services.LogService.Instance.Debug("OverlayApi", $"ShowMarker: parsed direction = {dir.Value}");

            EnsureOverlay();
            Services.LogService.Instance.Debug("OverlayApi", $"ShowMarker: overlay ensured, _overlay is null = {_overlay == null}");

            InvokeOnUI(() =>
            {
                Services.LogService.Instance.Debug("OverlayApi", $"ShowMarker: InvokeOnUI executing, _overlay is null = {_overlay == null}");
                _overlay?.Show();
                _overlay?.ShowDirectionMarker(dir.Value, duration);
            });
        }

        /// <summary>
        /// 清除所有方向标记
        /// </summary>
        public void ClearMarkers()
        {
            InvokeOnUI(() => _overlay?.ClearMarkers());
        }

        #endregion

        #region Edit Mode Methods

        /// <summary>
        /// 进入编辑模式
        /// 编辑模式下可拖拽移动和缩放覆盖层
        /// </summary>
        public void EnterEditMode()
        {
            EnsureOverlay();
            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    _overlay.Show();
                    _overlay.EnterEditMode();
                }
            });
        }

        /// <summary>
        /// 退出编辑模式
        /// 退出时自动保存位置和大小到配置
        /// </summary>
        public void ExitEditMode()
        {
            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    _overlay.ExitEditMode();
                    SaveOverlayConfig();
                }
            });
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 确保覆盖层窗口已创建
        /// </summary>
        private void EnsureOverlay()
        {
            Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay called, _overlay is null = {_overlay == null}");

            if (_overlay != null)
                return;

            InvokeOnUI(() =>
            {
                Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay: InvokeOnUI executing for plugin {_context.PluginId}");

                _overlay = OverlayManager.Instance.GetOverlay(_context.PluginId);
                Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay: GetOverlay returned {(_overlay == null ? "null" : "existing overlay")}");

                if (_overlay == null)
                {
                    // 从配置读取初始位置和大小
                    var x = _configApi.Get("overlay.x", 50);
                    var y = _configApi.Get("overlay.y", 50);
                    var width = _configApi.Get("overlay.width", 200);
                    var height = _configApi.Get("overlay.height", 200);

                    Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay: Creating overlay at ({x}, {y}) size ({width}, {height})");

                    var options = new OverlayOptions
                    {
                        X = Convert.ToDouble(x),
                        Y = Convert.ToDouble(y),
                        Width = Convert.ToDouble(width),
                        Height = Convert.ToDouble(height)
                    };

                    _overlay = OverlayManager.Instance.CreateOverlay(_context.PluginId, options);
                    Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay: CreateOverlay returned {(_overlay == null ? "null" : "new overlay")}");
                    
                    // 订阅编辑模式退出事件
                    if (_overlay != null)
                    {
                        _overlay.EditModeExited += OnEditModeExited;
                    }
                }
            });
        }

        /// <summary>
        /// 编辑模式退出事件处理
        /// </summary>
        private void OnEditModeExited(object? sender, EventArgs e)
        {
            SaveOverlayConfig();
        }

        /// <summary>
        /// 保存覆盖层配置
        /// </summary>
        private void SaveOverlayConfig()
        {
            if (_overlay == null)
                return;

            var rect = _overlay.GetRect();
            _configApi.Set("overlay.x", (int)rect.X);
            _configApi.Set("overlay.y", (int)rect.Y);
            _configApi.Set("overlay.width", (int)rect.Width);
            _configApi.Set("overlay.height", (int)rect.Height);
        }

        /// <summary>
        /// 解析方向字符串
        /// </summary>
        /// <param name="direction">方向字符串</param>
        /// <returns>方向枚举，无效返回 null</returns>
        private static Direction? ParseDirection(string direction)
        {
            return direction.ToLowerInvariant() switch
            {
                "north" or "n" or "up" => Direction.North,
                "northeast" or "ne" => Direction.NorthEast,
                "east" or "e" or "right" => Direction.East,
                "southeast" or "se" => Direction.SouthEast,
                "south" or "s" or "down" => Direction.South,
                "southwest" or "sw" => Direction.SouthWest,
                "west" or "w" or "left" => Direction.West,
                "northwest" or "nw" => Direction.NorthWest,
                _ => null
            };
        }

        /// <summary>
        /// 在 UI 线程上执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        private static void InvokeOnUI(Action action)
        {
            if (Application.Current?.Dispatcher == null)
                return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            if (_overlay != null)
            {
                _overlay.EditModeExited -= OnEditModeExited;
            }
            
            OverlayManager.Instance.DestroyOverlay(_context.PluginId);
            _overlay = null;
        }

        #endregion
    }
}
