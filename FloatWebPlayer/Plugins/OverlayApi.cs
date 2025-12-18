using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// 设置标记样式
        /// </summary>
        /// <param name="size">标记大小（像素），范围 16-64</param>
        /// <param name="color">标记颜色（十六进制，如 #FFFF6B6B）</param>
        public void SetMarkerStyle(double size, string color)
        {
            EnsureOverlay();
            InvokeOnUI(() => _overlay?.SetMarkerStyle(size, color));
        }

        /// <summary>
        /// 设置标记样式（从选项对象）
        /// </summary>
        /// <param name="options">样式选项对象，包含 size 和 color 属性</param>
        public void SetMarkerStyle(object? options)
        {
            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerStyle(object) called, options is null = {options == null}");
            
            if (options == null)
                return;

            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerStyle: options type = {options.GetType().FullName}");
            
            var dict = ConvertToDictionary(options);
            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerStyle: dict is null = {dict == null}");
            
            if (dict == null)
                return;

            double size = 24;
            string color = "#FFFF6B6B";

            if (dict.TryGetValue("size", out var sizeValue) && sizeValue != null)
            {
                Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerStyle: sizeValue = {sizeValue}, type = {sizeValue.GetType().Name}");
                size = Convert.ToDouble(sizeValue);
            }
            if (dict.TryGetValue("color", out var colorValue) && colorValue != null)
                color = colorValue.ToString() ?? "#FFFF6B6B";

            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerStyle: calling SetMarkerStyle({size}, {color})");
            SetMarkerStyle(size, color);
            Services.LogService.Instance.Debug("OverlayApi", "SetMarkerStyle: completed");
        }

        /// <summary>
        /// 设置标记图片
        /// </summary>
        /// <param name="path">图片路径（相对于插件目录或绝对路径），图片应指向右/东方向</param>
        /// <returns>是否设置成功</returns>
        public bool SetMarkerImage(string path)
        {
            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerImage called with path: {path}");
            Services.LogService.Instance.Debug("OverlayApi", $"Plugin directory: {_context.PluginDirectory}");

            if (string.IsNullOrEmpty(path))
            {
                Services.LogService.Instance.Warn("OverlayApi", "SetMarkerImage: path is null or empty");
                return false;
            }

            // 解析路径：如果是相对路径，则相对于插件目录
            string fullPath = path;
            if (!System.IO.Path.IsPathRooted(path))
            {
                fullPath = System.IO.Path.Combine(_context.PluginDirectory, path);
            }

            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerImage: resolved full path: {fullPath}");

            if (!System.IO.File.Exists(fullPath))
            {
                Services.LogService.Instance.Error("OverlayApi", $"SetMarkerImage: Image file not found: {fullPath}");
                return false;
            }

            Services.LogService.Instance.Debug("OverlayApi", "SetMarkerImage: Image file exists, ensuring overlay...");
            EnsureOverlay();
            bool result = false;

            InvokeOnUI(() =>
            {
                Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerImage: InvokeOnUI, _overlay is null = {_overlay == null}");
                if (_overlay != null)
                {
                    result = _overlay.SetMarkerImage(fullPath);
                    Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerImage: overlay.SetMarkerImage returned {result}");
                }
            });

            Services.LogService.Instance.Debug("OverlayApi", $"SetMarkerImage: returning {result}");
            return result;
        }

        #endregion

        #region Drawing Methods

        /// <summary>
        /// 绘制文本
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="options">样式选项（可选）</param>
        /// <returns>元素 ID</returns>
        public string DrawText(string text, double x, double y, object? options = null)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            EnsureOverlay();
            string elementId = string.Empty;

            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    var drawOptions = ParseDrawTextOptions(options);
                    elementId = _overlay.DrawText(text, x, y, drawOptions);
                }
            });

            return elementId;
        }

        /// <summary>
        /// 绘制矩形
        /// </summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="options">样式选项（可选）</param>
        /// <returns>元素 ID</returns>
        public string DrawRect(double x, double y, double width, double height, object? options = null)
        {
            if (width <= 0 || height <= 0)
                return string.Empty;

            EnsureOverlay();
            string elementId = string.Empty;

            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    var drawOptions = ParseDrawRectOptions(options);
                    elementId = _overlay.DrawRect(x, y, width, height, drawOptions);
                }
            });

            return elementId;
        }

        /// <summary>
        /// 绘制图片
        /// </summary>
        /// <param name="path">图片路径（相对于插件目录或绝对路径）</param>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        /// <param name="options">样式选项（可选）</param>
        /// <returns>元素 ID，失败返回空字符串</returns>
        public string DrawImage(string path, double x, double y, object? options = null)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // 解析路径：如果是相对路径，则相对于插件目录
            string fullPath = path;
            if (!System.IO.Path.IsPathRooted(path))
            {
                fullPath = System.IO.Path.Combine(_context.PluginDirectory, path);
            }

            if (!System.IO.File.Exists(fullPath))
            {
                Services.LogService.Instance.Error("OverlayApi", $"DrawImage: Image file not found: {fullPath}");
                return string.Empty;
            }

            EnsureOverlay();
            string elementId = string.Empty;

            InvokeOnUI(() =>
            {
                if (_overlay != null)
                {
                    var drawOptions = ParseDrawImageOptions(options);
                    elementId = _overlay.DrawImage(fullPath, x, y, drawOptions);
                }
            });

            return elementId;
        }

        /// <summary>
        /// 移除指定绘图元素
        /// </summary>
        /// <param name="elementId">元素 ID</param>
        public void RemoveElement(string elementId)
        {
            if (string.IsNullOrEmpty(elementId))
                return;

            InvokeOnUI(() =>
            {
                _overlay?.RemoveElement(elementId);
            });
        }

        /// <summary>
        /// 清除该插件的所有绘图元素
        /// </summary>
        public void Clear()
        {
            InvokeOnUI(() =>
            {
                _overlay?.ClearDrawingElements();
            });
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
                    // 使用 overlay.size 保持与 settings_ui.json 和 main.js 的一致性
                    var x = _configApi.Get("overlay.x", 50);
                    var y = _configApi.Get("overlay.y", 50);
                    var size = _configApi.Get("overlay.size", 200);

                    Services.LogService.Instance.Debug("OverlayApi", $"EnsureOverlay: Creating overlay at ({x}, {y}) size ({size})");

                    var options = new OverlayOptions
                    {
                        X = Convert.ToDouble(x),
                        Y = Convert.ToDouble(y),
                        Width = Convert.ToDouble(size),
                        Height = Convert.ToDouble(size)
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
            // 使用 overlay.size 保持与 settings_ui.json 和 main.js 的一致性
            // 覆盖层为正方形，使用 Width 作为 size
            _configApi.Set("overlay.size", (int)rect.Width);
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

        /// <summary>
        /// 解析 DrawText 选项
        /// </summary>
        private static DrawTextOptions ParseDrawTextOptions(object? options)
        {
            var result = new DrawTextOptions();
            if (options == null)
                return result;

            var dict = ConvertToDictionary(options);
            if (dict == null)
                return result;

            if (dict.TryGetValue("fontSize", out var fontSize))
                result.FontSize = Convert.ToDouble(fontSize);
            if (dict.TryGetValue("fontFamily", out var fontFamily))
                result.FontFamily = fontFamily?.ToString();
            if (dict.TryGetValue("color", out var color))
                result.Color = color?.ToString();
            if (dict.TryGetValue("backgroundColor", out var bgColor))
                result.BackgroundColor = bgColor?.ToString();
            if (dict.TryGetValue("opacity", out var opacity))
                result.Opacity = Convert.ToDouble(opacity);
            if (dict.TryGetValue("duration", out var duration))
                result.Duration = Convert.ToInt32(duration);

            return result;
        }

        /// <summary>
        /// 解析 DrawRect 选项
        /// </summary>
        private static DrawRectOptions ParseDrawRectOptions(object? options)
        {
            var result = new DrawRectOptions();
            if (options == null)
                return result;

            var dict = ConvertToDictionary(options);
            if (dict == null)
                return result;

            if (dict.TryGetValue("fill", out var fill))
                result.Fill = fill?.ToString();
            if (dict.TryGetValue("stroke", out var stroke))
                result.Stroke = stroke?.ToString();
            if (dict.TryGetValue("strokeWidth", out var strokeWidth))
                result.StrokeWidth = Convert.ToDouble(strokeWidth);
            if (dict.TryGetValue("opacity", out var opacity))
                result.Opacity = Convert.ToDouble(opacity);
            if (dict.TryGetValue("cornerRadius", out var cornerRadius))
                result.CornerRadius = Convert.ToDouble(cornerRadius);
            if (dict.TryGetValue("duration", out var duration))
                result.Duration = Convert.ToInt32(duration);

            return result;
        }

        /// <summary>
        /// 解析 DrawImage 选项
        /// </summary>
        private static DrawImageOptions ParseDrawImageOptions(object? options)
        {
            var result = new DrawImageOptions();
            if (options == null)
                return result;

            var dict = ConvertToDictionary(options);
            if (dict == null)
                return result;

            if (dict.TryGetValue("width", out var width))
                result.Width = Convert.ToDouble(width);
            if (dict.TryGetValue("height", out var height))
                result.Height = Convert.ToDouble(height);
            if (dict.TryGetValue("opacity", out var opacity))
                result.Opacity = Convert.ToDouble(opacity);
            if (dict.TryGetValue("duration", out var duration))
                result.Duration = Convert.ToInt32(duration);

            return result;
        }

        /// <summary>
        /// 将对象转换为字典（支持 Jint 对象和匿名对象）
        /// </summary>
        private static Dictionary<string, object?>? ConvertToDictionary(object? obj)
        {
            if (obj == null)
                return null;

            // 如果已经是字典
            if (obj is Dictionary<string, object?> dict)
                return dict;

            if (obj is IDictionary<string, object> idict)
                return idict.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            // 尝试从 Jint ObjectInstance 获取属性
            var type = obj.GetType();
            if (type.FullName?.Contains("Jint") == true)
            {
                try
                {
                    var result = new Dictionary<string, object?>();
                    
                    // 使用反射获取 Jint 对象的属性
                    var getOwnPropertyKeysMethod = type.GetMethod("GetOwnPropertyKeys");
                    if (getOwnPropertyKeysMethod != null)
                    {
                        var keys = getOwnPropertyKeysMethod.Invoke(obj, new object[] { 0 }) as IEnumerable<object>;
                        if (keys != null)
                        {
                            var getMethod = type.GetMethod("Get", new[] { typeof(string) });
                            foreach (var key in keys)
                            {
                                var keyStr = key.ToString();
                                if (keyStr != null && getMethod != null)
                                {
                                    var value = getMethod.Invoke(obj, new object[] { keyStr });
                                    result[keyStr] = ConvertJintValue(value);
                                }
                            }
                        }
                    }
                    
                    return result.Count > 0 ? result : null;
                }
                catch
                {
                    return null;
                }
            }

            // 尝试从匿名对象获取属性
            try
            {
                var result = new Dictionary<string, object?>();
                foreach (var prop in type.GetProperties())
                {
                    result[prop.Name] = prop.GetValue(obj);
                }
                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 转换 Jint 值为 .NET 值
        /// </summary>
        private static object? ConvertJintValue(object? value)
        {
            if (value == null)
                return null;

            var type = value.GetType();
            
            // Jint JsNumber
            if (type.Name == "JsNumber" || type.Name == "JsValue")
            {
                var toObjectMethod = type.GetMethod("ToObject");
                if (toObjectMethod != null)
                {
                    return toObjectMethod.Invoke(value, null);
                }
            }

            // Jint JsString
            if (type.Name == "JsString")
            {
                return value.ToString();
            }

            return value;
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
