using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FloatWebPlayer.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// LogService 属性测试
    /// </summary>
    public class LogServiceTests
    {
        /// <summary>
        /// **Feature: logging-system, Property 1: 单例一致性**
        /// *对于任意*次数的 LogService.Instance 调用，所有调用应返回相同的对象引用
        /// **Validates: Requirements 1.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property Singleton_ShouldReturnSameInstance(PositiveInt callCount)
        {
            // 限制调用次数在合理范围内
            var count = Math.Min(callCount.Get, 1000);
            
            var instances = new LogService[count];
            for (int i = 0; i < count; i++)
            {
                instances[i] = LogService.Instance;
            }

            // 验证所有实例都是同一个对象
            var firstInstance = instances[0];
            var allSame = instances.All(inst => ReferenceEquals(inst, firstInstance));

            return allSame
                .Label($"所有 {count} 次调用应返回相同实例");
        }

        /// <summary>
        /// **Feature: logging-system, Property 2: 日志文件名格式**
        /// *对于任意*日期，生成的日志文件名应匹配模式 float-web-player-yyyyMMdd.log
        /// **Validates: Requirements 2.2**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property LogFileName_ShouldMatchPattern(int year, int month, int day)
        {
            // 生成有效日期范围
            var validYear = Math.Abs(year % 100) + 2000; // 2000-2099
            var validMonth = (Math.Abs(month) % 12) + 1; // 1-12
            var maxDay = DateTime.DaysInMonth(validYear, validMonth);
            var validDay = (Math.Abs(day) % maxDay) + 1; // 1-maxDay

            var date = new DateTime(validYear, validMonth, validDay);
            
            // 创建临时 LogService 实例用于测试
            var tempDir = Path.GetTempPath();
            var logService = new LogService(tempDir);
            var filePath = logService.GetLogFilePath(date);
            var fileName = Path.GetFileName(filePath);

            // 验证文件名格式
            var pattern = @"^float-web-player-\d{8}\.log$";
            var matchesPattern = Regex.IsMatch(fileName, pattern);

            // 验证日期部分正确
            var expectedFileName = $"float-web-player-{date:yyyyMMdd}.log";
            var dateCorrect = fileName == expectedFileName;

            return (matchesPattern && dateCorrect)
                .Label($"文件名 '{fileName}' 应匹配模式且日期正确 (期望: {expectedFileName})");
        }


        /// <summary>
        /// **Feature: logging-system, Property 3: 日志条目格式**
        /// *对于任意*日志级别、来源字符串和消息字符串，格式化的日志条目应匹配模式 [yyyy-MM-dd HH:mm:ss.fff] [级别] [来源] 消息
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property LogEntry_ShouldMatchFormat(
            int levelIndex, NonEmptyString source, NonEmptyString message)
        {
            // 生成有效的日志级别
            var level = (LogLevel)(Math.Abs(levelIndex) % 4);
            
            // 排除包含换行符的来源和消息（这些会破坏单行格式）
            var sourceStr = source.Get.Replace("\n", "").Replace("\r", "");
            var messageStr = message.Get.Replace("\n", "").Replace("\r", "");
            
            if (string.IsNullOrWhiteSpace(sourceStr) || string.IsNullOrWhiteSpace(messageStr))
            {
                return true.ToProperty(); // 跳过无效输入
            }

            var tempDir = Path.GetTempPath();
            var logService = new LogService(tempDir);
            var timestamp = DateTime.Now;
            var entry = logService.FormatLogEntry(timestamp, level, sourceStr, messageStr);

            // 验证格式：[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [source] message
            var pattern = @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[[A-Z]+\] \[.+\] .+$";
            var matchesPattern = Regex.IsMatch(entry, pattern);

            // 验证时间戳格式正确
            var expectedTimestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var hasCorrectTimestamp = entry.StartsWith($"[{expectedTimestamp}]");

            return (matchesPattern && hasCorrectTimestamp)
                .Label($"条目 '{entry}' 应匹配格式模式");
        }

        /// <summary>
        /// **Feature: logging-system, Property 4: 日志级别大写**
        /// *对于任意*日志级别，格式化输出应包含大写的级别名称（DEBUG、INFO、WARN、ERROR）
        /// **Validates: Requirements 3.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property LogLevel_ShouldBeUppercase(int levelIndex)
        {
            var level = (LogLevel)(Math.Abs(levelIndex) % 4);
            
            var tempDir = Path.GetTempPath();
            var logService = new LogService(tempDir);
            var entry = logService.FormatLogEntry(DateTime.Now, level, "TestSource", "TestMessage");

            // 获取期望的大写级别名称
            var expectedLevelStr = level.ToString().ToUpperInvariant();
            
            // 验证条目包含大写的级别名称
            var containsUppercaseLevel = entry.Contains($"[{expectedLevelStr}]");

            return containsUppercaseLevel
                .Label($"条目应包含大写级别 [{expectedLevelStr}]，实际: {entry}");
        }


        /// <summary>
        /// **Feature: logging-system, Property 5: 来源保留**
        /// *对于任意*来源字符串，格式化的日志条目应包含该确切的来源字符串
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property Source_ShouldBePreserved(NonEmptyString source)
        {
            // 排除包含方括号的来源（会干扰格式解析）
            var sourceStr = source.Get.Replace("[", "").Replace("]", "").Replace("\n", "").Replace("\r", "");
            
            if (string.IsNullOrWhiteSpace(sourceStr))
            {
                return true.ToProperty(); // 跳过无效输入
            }

            var tempDir = Path.GetTempPath();
            var logService = new LogService(tempDir);
            var entry = logService.FormatLogEntry(DateTime.Now, LogLevel.Info, sourceStr, "TestMessage");

            // 验证条目包含来源字符串
            var containsSource = entry.Contains($"[{sourceStr}]");

            return containsSource
                .Label($"条目应包含来源 [{sourceStr}]，实际: {entry}");
        }

        /// <summary>
        /// **Feature: logging-system, Property 6: 并发写入安全**
        /// *对于任意*来自多个线程的并发日志写入集合，所有日志条目应被写入而不丢失数据或损坏
        /// **Validates: Requirements 4.1**
        /// </summary>
        [Property(MaxTest = 10)]
        public Property ConcurrentWrites_ShouldNotLoseData(PositiveInt threadCount, PositiveInt messagesPerThread)
        {
            // 限制线程数和消息数在合理范围内
            var threads = Math.Min(threadCount.Get, 10);
            var messages = Math.Min(messagesPerThread.Get, 50);
            
            // 创建临时目录用于测试
            var tempDir = Path.Combine(Path.GetTempPath(), $"LogServiceTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var logService = new LogService(tempDir);
                var expectedMessages = new ConcurrentBag<string>();
                var tasks = new Task[threads];

                // 启动多个线程并发写入
                for (int t = 0; t < threads; t++)
                {
                    var threadId = t;
                    tasks[t] = Task.Run(() =>
                    {
                        for (int m = 0; m < messages; m++)
                        {
                            var msg = $"Thread{threadId}_Message{m}";
                            expectedMessages.Add(msg);
                            logService.Log(LogLevel.Info, $"Thread{threadId}", msg);
                        }
                    });
                }

                // 等待所有任务完成
                Task.WaitAll(tasks);

                // 读取日志文件内容
                var logFile = logService.GetLogFilePath();
                if (!File.Exists(logFile))
                {
                    return false.Label("日志文件不存在");
                }

                var logContent = File.ReadAllText(logFile);

                // 验证所有消息都被写入
                var allMessagesWritten = expectedMessages.All(msg => logContent.Contains(msg));
                var expectedCount = threads * messages;
                var actualLineCount = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

                return (allMessagesWritten && actualLineCount >= expectedCount)
                    .Label($"期望 {expectedCount} 条消息，实际 {actualLineCount} 行，所有消息都写入: {allMessagesWritten}");
            }
            finally
            {
                // 清理临时目录
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }
    }
}
