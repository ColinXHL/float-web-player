using System;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis.Core
{
/// <summary>
/// 核心 API
/// 提供日志输出和版本信息
/// 无需权限
/// </summary>
public class CoreApi
{
#region Fields

    private readonly PluginContext _context;
    private readonly LogProxy _logProxy;

#endregion

#region Properties

    /// <summary>
    /// 主程序版本
    /// </summary>
    [ScriptMember("version")]
    public string Version => AppConstants.Version;

    /// <summary>
    /// 日志代理对象，提供 debug/info/warn/error 方法
    /// 使用方式：core.logger.info("message") 或 core.logger.debug("template {Param}", value)
    /// </summary>
    [ScriptMember("logger")]
    public LogProxy Logger => _logProxy;

#endregion

#region Constructor

    /// <summary>
    /// 创建核心 API 实例
    /// </summary>
    /// <param name="context">插件上下文</param>
    public CoreApi(PluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logProxy = new LogProxy(context.PluginId);
    }

#endregion
}
}
