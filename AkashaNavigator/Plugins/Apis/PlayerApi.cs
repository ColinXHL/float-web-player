using System;
using System.Threading.Tasks;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Player API
/// </summary>
public class PlayerApi
{
    private readonly PluginContext _context;
    private readonly Func<Views.Windows.PlayerWindow?>? _getPlayerWindow;
    private EventManager? _eventManager;

    public PlayerApi(PluginContext context, Func<Views.Windows.PlayerWindow?>? getPlayerWindow)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _getPlayerWindow = getPlayerWindow;
    }

    public void SetEventManager(EventManager eventManager) => _eventManager = eventManager;

    /// <summary>
    /// 当前 URL（小写属性名，JavaScript 风格）
    /// </summary>
    public string url => _getPlayerWindow?.Invoke()?.CurrentUrl ?? string.Empty;

    /// <summary>
    /// 当前播放时间（秒）
    /// </summary>
    public double currentTime => 0; // TODO: 从 WebView 获取

    /// <summary>
    /// 视频总时长（秒）
    /// </summary>
    public double duration => 0; // TODO: 从 WebView 获取

    /// <summary>
    /// 当前音量（0.0-1.0）
    /// </summary>
    public double volume => 1.0; // TODO: 从 WebView 获取

    /// <summary>
    /// 当前播放速度
    /// </summary>
    public double playbackRate => 1.0; // TODO: 从 WebView 获取

    /// <summary>
    /// 是否静音
    /// </summary>
    public bool muted => false; // TODO: 从 WebView 获取

    /// <summary>
    /// 是否正在播放
    /// </summary>
    public bool playing => false; // TODO: 从 WebView 获取

    /// <summary>
    /// 开始播放
    /// </summary>
    public void play()
    {
        _getPlayerWindow?.Invoke()?.TogglePlayAsync();
        _eventManager?.Emit(EventManager.PlayStateChanged, new { playing = true });
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void pause()
    {
        _getPlayerWindow?.Invoke()?.TogglePlayAsync();
        _eventManager?.Emit(EventManager.PlayStateChanged, new { playing = false });
    }

    /// <summary>
    /// 跳转到指定时间
    /// </summary>
    /// <param name="time">目标时间（秒）</param>
    public void seek(double time) => _getPlayerWindow?.Invoke()?.SeekAsync((int)time);

    /// <summary>
    /// 设置音量
    /// </summary>
    /// <param name="vol">音量（0.0-1.0）</param>
    public void setVolume(double vol)
    { /* TODO: 实现音量控制 */
    }

    /// <summary>
    /// 设置播放速度
    /// </summary>
    /// <param name="rate">播放速度</param>
    public void setPlaybackRate(double rate)
    { /* TODO: 实现播放速度控制 */
    }

    /// <summary>
    /// 设置静音状态
    /// </summary>
    /// <param name="mute">是否静音</param>
    public void setMuted(bool mute)
    { /* TODO: 实现静音控制 */
    }

    /// <summary>
    /// 导航到指定 URL
    /// </summary>
    /// <param name="targetUrl">目标 URL</param>
    /// <returns>Task</returns>
    public Task navigate(string targetUrl)
    {
        _getPlayerWindow?.Invoke()?.Navigate(targetUrl);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新当前页面
    /// </summary>
    /// <returns>Task</returns>
    public Task reload()
    {
        _getPlayerWindow?.Invoke()?.Refresh();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注册事件监听器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="callback">回调函数</param>
    /// <returns>监听器 ID</returns>
    public int on(string eventName, object callback)
    {
        return _eventManager?.On($"player.{eventName}", callback) ?? -1;
    }

    /// <summary>
    /// 取消事件监听
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="id">监听器 ID（可选）</param>
    public void off(string eventName, int? id = null)
    {
        if (id.HasValue)
            _eventManager?.Off(id.Value);
        else
            _eventManager?.Off($"player.{eventName}");
    }
}
}
