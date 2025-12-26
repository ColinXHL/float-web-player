using AkashaNavigator.Plugins.Utils;

namespace AkashaNavigator.Plugins.Apis.Core
{
/// <summary>
/// Event API
/// </summary>
public class EventApi
{
    private readonly PluginContext _context;
    private readonly EventManager _eventManager;

    public EventApi(PluginContext context, EventManager eventManager)
    {
        _context = context;
        _eventManager = eventManager;
    }

    public int on(string eventName, object callback)
    {
        return _eventManager.On(eventName, callback);
    }

    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager.Off(id.Value);
        else
            _eventManager.Off(eventName);
    }

    public void emit(string eventName, object? data = null)
    {
        _eventManager.Emit(eventName, data);
    }
}
}
