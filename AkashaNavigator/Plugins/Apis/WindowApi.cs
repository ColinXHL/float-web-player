using AkashaNavigator.Views.Windows;
using AkashaNavigator.Plugins.Utils;

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
        _context = context;
        _getPlayerWindow = getPlayerWindow;
    }

    public void SetEventManager(EventManager eventManager) => _eventManager = eventManager;

    public double Opacity => _getPlayerWindow?.Invoke()?.Opacity ?? 1.0;
    public bool ClickThrough => _getPlayerWindow?.Invoke()?.IsClickThrough ?? false;
    public bool Topmost => _getPlayerWindow?.Invoke()?.Topmost ?? true;

    public object Bounds
    {
        get {
            var window = _getPlayerWindow?.Invoke();
            if (window == null)
                return new { x = 0, y = 0, width = 0, height = 0 };
            return new { x = (int)window.Left, y = (int)window.Top, width = (int)window.Width,
                         height = (int)window.Height };
        }
    }

    public void SetOpacity(double opacity)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Opacity = opacity);
    }

    public void SetClickThrough(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null && window.IsClickThrough != enabled)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.ToggleClickThrough());
    }

    public void SetTopmost(bool enabled)
    {
        var window = _getPlayerWindow?.Invoke();
        if (window != null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => window.Topmost = enabled);
    }

    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"window.{eventName}", callback) ?? -1;
    }

    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"window.{eventName}");
    }
}
}
