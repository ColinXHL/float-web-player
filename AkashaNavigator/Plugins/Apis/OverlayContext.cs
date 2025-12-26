using System;
using System.Linq;
using AkashaNavigator.Services;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Overlay 绘图上下文
/// 提供 Canvas-like 绘图 API
/// </summary>
public class OverlayContext
{
    private readonly string _pluginId;

    // 绘图状态（小写属性名，Canvas 风格）
    public string fillStyle { get; set; } = "#000000";
    public string strokeStyle { get; set; } = "#000000";
    public double lineWidth { get; set; } = 1.0;
    public string font { get; set; } = "14px Arial";

    public OverlayContext(string pluginId)
    {
        _pluginId = pluginId;
    }

    /// <summary>
    /// 填充矩形
    /// </summary>
    public void fillRect(double x, double y, double width, double height)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                                              {
                                                                  var options = new DrawRectOptions { Fill = fillStyle,
                                                                                                      Opacity = 1.0 };
                                                                  overlay.DrawRect(x, y, width, height, options);
                                                              });
    }

    /// <summary>
    /// 描边矩形
    /// </summary>
    public void strokeRect(double x, double y, double width, double height)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                var options = new DrawRectOptions { Stroke = strokeStyle, StrokeWidth = lineWidth, Opacity = 1.0 };
                overlay.DrawRect(x, y, width, height, options);
            });
    }

    /// <summary>
    /// 填充文本
    /// </summary>
    public void fillText(string text, double x, double y)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                // 解析字体大小
                double fontSize = 14;
                string fontFamily = "Arial";
                if (!string.IsNullOrEmpty(font))
                {
                    var parts = font.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var sizeStr = parts[0].Replace("px", "").Replace("pt", "");
                        if (double.TryParse(sizeStr, out var size))
                            fontSize = size;
                        fontFamily = string.Join(" ", parts.Skip(1));
                    }
                }

                var options = new DrawTextOptions { Color = fillStyle, FontSize = fontSize, FontFamily = fontFamily,
                                                    Opacity = 1.0 };
                overlay.DrawText(text, x, y, options);
            });
    }

    /// <summary>
    /// 绘制图片
    /// </summary>
    public void drawImage(string src, double x, double y, double? width = null, double? height = null)
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        if (overlay == null)
            return;

        System.Windows.Application.Current?.Dispatcher.Invoke(
            () =>
            {
                var options = new DrawImageOptions { Width = width ?? 0, Height = height ?? 0, Opacity = 1.0 };
                overlay.DrawImage(src, x, y, options);
            });
    }

    /// <summary>
    /// 清除画布
    /// </summary>
    public void clear()
    {
        var overlay = OverlayManager.Instance.GetOverlay(_pluginId);
        System.Windows.Application.Current?.Dispatcher.Invoke(() => overlay?.ClearDrawingElements());
    }
}
}
