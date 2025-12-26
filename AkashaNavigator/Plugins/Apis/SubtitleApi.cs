using System;
using System.Linq;
using AkashaNavigator.Models.Data;
using AkashaNavigator.Services;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Subtitle API
/// </summary>
public class SubtitleApi
{
    private readonly PluginContext _context;
    private readonly V8ScriptEngine _engine;
    private EventManager? _eventManager;
    private bool _isSubscribed;

    public SubtitleApi(PluginContext context, V8ScriptEngine engine)
    {
        _context = context;
        _engine = engine;
    }

    public void SetEventManager(EventManager eventManager)
    {
        _eventManager = eventManager;
        SubscribeToService();
    }

    /// <summary>
    /// 订阅 SubtitleService 事件
    /// </summary>
    private void SubscribeToService()
    {
        if (_isSubscribed || _eventManager == null)
            return;

        SubtitleService.Instance.SubtitleChanged += OnSubtitleChanged;
        SubtitleService.Instance.SubtitleLoaded += OnSubtitleLoaded;
        SubtitleService.Instance.SubtitleCleared += OnSubtitleCleared;
        _isSubscribed = true;

        LogService.Instance.Debug($"Plugin:{_context.PluginId}", "SubtitleApi: EventManager set");
    }

    /// <summary>
    /// 取消订阅（清理时调用）
    /// </summary>
    public void Cleanup()
    {
        if (!_isSubscribed)
            return;

        SubtitleService.Instance.SubtitleChanged -= OnSubtitleChanged;
        SubtitleService.Instance.SubtitleLoaded -= OnSubtitleLoaded;
        SubtitleService.Instance.SubtitleCleared -= OnSubtitleCleared;
        _isSubscribed = false;
    }

    private void OnSubtitleChanged(object? sender, SubtitleEntry? e)
    {
        var jsEntry = e != null ? new { from = e.From, to = e.To, content = e.Content } : null;
        _eventManager?.Emit("subtitle.change", jsEntry);
    }

    private void OnSubtitleLoaded(object? sender, SubtitleData data)
    {
        // 创建原生 JS 数组，确保 forEach 等方法可用
        var jsBody = CreateJsArray(data.Body);
        var jsData = new { language = data.Language, body = jsBody, sourceUrl = data.SourceUrl };
        _eventManager?.Emit("subtitle.load", jsData);
    }

    /// <summary>
    /// 创建原生 JS 数组
    /// </summary>
    private object CreateJsArray(IEnumerable<SubtitleEntry> entries)
    {
        try
        {
            // 使用 V8 引擎创建原生 JS 数组
            dynamic jsArray = _engine.Evaluate("[]");
            foreach (var entry in entries)
            {
                jsArray.push(new { from = entry.From, to = entry.To, content = entry.Content });
            }
            return jsArray;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", "CreateJsArray failed: {ErrorMessage}",
                                      ex.Message);
            // 回退到 C# 数组
            return entries.Select(e => (object) new { from = e.From, to = e.To, content = e.Content }).ToArray();
        }
    }

    private void OnSubtitleCleared(object? sender, EventArgs e)
    {
        _eventManager?.Emit("subtitle.clear", null);
    }

    // 属性和方法
    public bool hasSubtitles => SubtitleService.Instance.GetSubtitleData() != null;

    public object? getCurrent(double? time = null)
    {
        var entry = time.HasValue ? SubtitleService.Instance.GetSubtitleAt(time.Value) : null;
        if (entry == null)
            return null;
        return new { from = entry.From, to = entry.To, content = entry.Content };
    }

    public object getAll()
    {
        var entries = SubtitleService.Instance.GetAllSubtitles();
        return CreateJsArray(entries);
    }

    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"subtitle.{eventName}", callback) ?? -1;
    }

    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"subtitle.{eventName}");
    }
}
}
