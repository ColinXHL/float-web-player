using AkashaNavigator.Views.Windows;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Window API
/// </summary>
public class WindowApi
{
    private readonly PluginContext _context;
    private readonly Func<Views.Windows.PlayerWindow?>? _getPlayerWindow;
    private EventManager? _eventManager;

    public WindowApi(PluginContext context, Func<Views.Windows.PlayerWindow?>? getPlayerWindow)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _getPlayerWindow = getPlayerWindow;
    }

    public void SetEventManager(EventManager eventManager) => _eventManager = eventManager;

    [ScriptMember("getOpacity")]
    public double GetOpacity() => _getPlayerWindow?.Invoke()?.Opacity ?? 1.0;

    [ScriptMember("isClickThrough")]
    public bool IsClickThrough() => _getPlayerWindow?.Invoke()?.IsClickThrough ?? false;

    [ScriptMember("isTopmost")]
    public bool IsTopmost() => _getPlayerWindow?.Invoke()?.Topmost ?? true;

    [ScriptMember("getBounds")]
    public object GetBounds()
    {
        var window = _getPlayerWindow?.Invoke();
        if (window == null)
            return new { x = 0.0, y = 0.0, width = 0.0, height = 0.0 };
        return new { x = (double)window.Left, y = (double)window.Top, width = (double)window.Width,
                     height = (double)window.Height };
    }

    [ScriptMember("setOpacity")]
    public void SetOpacity(double opacity)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Opacity = opacity);
    }

    [ScriptMember("setClickThrough")]
    public void SetClickThrough(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null && window.IsClickThrough != enabled)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.ToggleClickThrough());
    }

    [ScriptMember("setTopmost")]
    public void SetTopmost(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Topmost = enabled);
    }

    [ScriptMember("on")]
    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"window.{eventName}", callback) ?? -1;
    }

    [ScriptMember("off")]
    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"window.{eventName}");
    }
}
}
