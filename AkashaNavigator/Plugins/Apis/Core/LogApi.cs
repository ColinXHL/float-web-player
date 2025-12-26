using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins.Apis.Core
{
/// <summary>
/// 日志 API
/// </summary>
public class LogApi
{
    private readonly string _pluginId;

    public LogApi(string pluginId)
    {
        _pluginId = pluginId;
    }

    /// <summary>
    /// 输出信息日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void info(string message) => LogService.Instance.Info($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出警告日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void warn(string message) => LogService.Instance.Warn($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出错误日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void error(string message) => LogService.Instance.Error($"Plugin:{_pluginId}", message);

    /// <summary>
    /// 输出调试日志
    /// </summary>
    /// <param name="message">日志内容</param>
    public void debug(string message) => LogService.Instance.Debug($"Plugin:{_pluginId}", message);
}
}
