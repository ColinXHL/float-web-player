using System;
using System.Diagnostics;
using System.IO;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    /// <summary>
    /// 日志服务（单例）
    /// 负责将日志写入文件，支持多级别日志记录，线程安全
    /// </summary>
    public class LogService
    {
        #region Singleton

        private static LogService? _instance;
        private static readonly object _singletonLock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static LogService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_singletonLock)
                    {
                        _instance ??= new LogService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private readonly object _writeLock = new();
        private string _currentLogDate = string.Empty;
        private string _currentLogFilePath = string.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// 当前最小日志级别（低于此级别的日志不记录）
        /// </summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Debug;

        /// <summary>
        /// 日志目录路径
        /// </summary>
        public string LogDirectory { get; }

        #endregion

        #region Constructor

        private LogService()
        {
            // 日志目录：exe 同级目录
            LogDirectory = GetLogDirectory();
        }

        /// <summary>
        /// 仅用于测试的内部构造函数
        /// </summary>
        internal LogService(string logDirectory)
        {
            LogDirectory = logDirectory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 核心日志方法
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="source">来源组件名称</param>
        /// <param name="message">日志消息</param>
        public void Log(LogLevel level, string source, string message)
        {
            if (level < MinLevel)
                return;

            var entry = FormatLogEntry(DateTime.Now, level, source, message);
            WriteToFile(entry);
        }

        /// <summary>
        /// 记录 Debug 级别日志
        /// </summary>
        public void Debug(string source, string message)
        {
            Log(LogLevel.Debug, source, message);
        }

        /// <summary>
        /// 记录 Info 级别日志
        /// </summary>
        public void Info(string source, string message)
        {
            Log(LogLevel.Info, source, message);
        }

        /// <summary>
        /// 记录 Warn 级别日志
        /// </summary>
        public void Warn(string source, string message)
        {
            Log(LogLevel.Warn, source, message);
        }

        /// <summary>
        /// 记录 Error 级别日志
        /// </summary>
        public void Error(string source, string message)
        {
            Log(LogLevel.Error, source, message);
        }

        /// <summary>
        /// 记录 Error 级别日志（包含异常信息）
        /// </summary>
        public void Error(string source, string message, Exception ex)
        {
            var fullMessage = $"{message}: {ex.GetType().Name} - {ex.Message}";
            if (ex.StackTrace != null)
            {
                fullMessage += $"\n{ex.StackTrace}";
            }
            Log(LogLevel.Error, source, fullMessage);
        }

        #endregion

        #region Internal Methods (for testing)

        /// <summary>
        /// 获取指定日期的日志文件路径
        /// </summary>
        internal string GetLogFilePath(DateTime date)
        {
            var fileName = $"float-web-player-{date:yyyyMMdd}.log";
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
        /// 格式化日志条目
        /// </summary>
        internal string FormatLogEntry(DateTime timestamp, LogLevel level, string source, string message)
        {
            var levelStr = level.ToString().ToUpperInvariant();
            return $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] [{source}] {message}";
        }

        #endregion

        #region Private Methods

        private static string GetLogDirectory()
        {
            try
            {
                // 日志放在 exe 同级目录的 logs 子文件夹
                return Path.Combine(AppContext.BaseDirectory, "logs");
            }
            catch
            {
                // 如果无法获取 exe 目录，使用当前工作目录的 logs 子文件夹
                return Path.Combine(Environment.CurrentDirectory, "logs");
            }
        }

        private void WriteToFile(string entry)
        {
            lock (_writeLock)
            {
                try
                {
                    var today = DateTime.Now.ToString("yyyyMMdd");
                    
                    // 检查是否需要切换到新的日志文件（新的一天）
                    if (_currentLogDate != today)
                    {
                        _currentLogDate = today;
                        _currentLogFilePath = GetLogFilePath(DateTime.Now);
                    }

                    // 确保目录存在
                    var dir = Path.GetDirectoryName(_currentLogFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    // 追加写入日志文件
                    File.AppendAllText(_currentLogFilePath, entry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // 文件写入失败，回退到 Debug.WriteLine
                    System.Diagnostics.Debug.WriteLine($"[LogService] 写入日志文件失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(entry);
                }
            }
        }

        #endregion
    }
}
