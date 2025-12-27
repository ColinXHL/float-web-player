using System;
using System.IO;
using Serilog;
using Serilog.Events;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 日志服务（Serilog Facade）
/// </summary>
public class LogService : ILogService
{
#region Singleton

    private static LogService? _instance;

    public static LogService Instance
    {
        get
        {
            _instance ??= new LogService();
            return _instance;
        }
        set => _instance = value;
    }

#endregion

#region Properties

    public string LogDirectory { get; }

#endregion

#region Constructor

    public LogService()
    {
        LogDirectory = GetLogDirectory();
    }

    public LogService(string logDirectory)
    {
        LogDirectory = logDirectory;
    }

#endregion

#region Public Methods - 参数化日志方法

    /// <summary>
    /// 记录 Debug 级别日志
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="message">日志消息</param>
    public void Debug(string source, string message)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Debug(message);
    }

    /// <summary>
    /// 记录 Debug 级别日志（参数化模板）
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="template">消息模板，支持 {Name} 占位符</param>
    /// <param name="args">模板参数</param>
    public void Debug(string source, string template, params object?[] args)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Debug(template, args);
    }

    /// <summary>
    /// 记录 Info 级别日志
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="message">日志消息</param>
    public void Info(string source, string message)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Information(message);
    }

    /// <summary>
    /// 记录 Info 级别日志（参数化模板）
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="template">消息模板，支持 {Name} 占位符</param>
    /// <param name="args">模板参数</param>
    public void Info(string source, string template, params object?[] args)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Information(template, args);
    }

    /// <summary>
    /// 记录 Warn 级别日志
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="message">日志消息</param>
    public void Warn(string source, string message)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Warning(message);
    }

    /// <summary>
    /// 记录 Warn 级别日志（参数化模板）
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="template">消息模板，支持 {Name} 占位符</param>
    /// <param name="args">模板参数</param>
    public void Warn(string source, string template, params object?[] args)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Warning(template, args);
    }

    /// <summary>
    /// 记录 Error 级别日志
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="message">日志消息</param>
    public void Error(string source, string message)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Error(message);
    }

    /// <summary>
    /// 记录 Error 级别日志（参数化模板）
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="template">消息模板，支持 {Name} 占位符</param>
    /// <param name="args">模板参数</param>
    public void Error(string source, string template, params object?[] args)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Error(template, args);
    }

    /// <summary>
    /// 记录 Error 级别日志（包含异常和参数化模板）
    /// </summary>
    /// <param name="source">来源组件名称</param>
    /// <param name="ex">异常对象</param>
    /// <param name="template">消息模板，支持 {Name} 占位符</param>
    /// <param name="args">模板参数</param>
    public void Error(string source, Exception ex, string template, params object?[] args)
    {
        var logger = Serilog.Log.ForContext("SourceContext", source);
        logger.Error(ex, template, args);
    }

#endregion

#region Internal Methods(for testing)

    /// <summary>
    /// 获取指定日期的日志文件路径
    /// </summary>
    internal string GetLogFilePath(DateTime date)
    {
        var fileName = $"akasha-navigator-{date:yyyyMMdd}.log";
        return Path.Combine(LogDirectory, fileName);
    }

    /// <summary>
    /// 获取当前日志文件路径
    /// </summary>
    internal string GetLogFilePath()
    {
        return GetLogFilePath(DateTime.Now);
    }

    /// <summary>
    /// 格式化日志条目（保留用于测试兼容性）
    /// </summary>
    internal string FormatLogEntry(DateTime timestamp, LogEventLevel level, string source, string message)
    {
        var levelStr = level switch { LogEventLevel.Debug => "DEBUG", LogEventLevel.Information => "INFO",
                                      LogEventLevel.Warning => "WARN", LogEventLevel.Error => "ERROR",
                                      _ => "INFO" };
        return $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] [{source}] {message}";
    }

#endregion

#region Private Methods

    private static string GetLogDirectory()
    {
        try
        {
            return Path.Combine(AppContext.BaseDirectory, "logs");
        }
        catch
        {
            return Path.Combine(Environment.CurrentDirectory, "logs");
        }
    }

#endregion
}
}
