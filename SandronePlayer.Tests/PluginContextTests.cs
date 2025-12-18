using System;
using System.IO;
using SandronePlayer.Models;
using SandronePlayer.Plugins;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// PluginContext 属性测试
    /// </summary>
    public class PluginContextTests : IDisposable
    {
        private readonly string _tempDir;

        public PluginContextTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"plugin_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }

        /// <summary>
        /// 创建测试用的插件清单
        /// </summary>
        private PluginManifest CreateTestManifest(string id = "test-plugin")
        {
            return new PluginManifest
            {
                Id = id,
                Name = "Test Plugin",
                Version = "1.0.0",
                Main = "main.js"
            };
        }

        /// <summary>
        /// 创建测试用的 JavaScript 文件
        /// </summary>
        private void CreateJsFile(string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, "main.js"), content);
        }


        #region Property 2: 异常隔离

        /// <summary>
        /// **Feature: game-plugin-system, Property 2: 异常隔离**
        /// *对于任意*抛出异常的插件代码，主程序应继续正常运行，其他插件不受影响
        /// **Validates: Requirements 1.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property PluginException_ShouldNotCrashHost(NonEmptyString errorMessage)
        {
            // 排除包含特殊字符的错误消息（可能导致 JS 语法错误）
            var safeMessage = errorMessage.Get
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            // 创建一个会抛出异常的插件脚本
            var jsCode = $@"
function onLoad(api) {{
    throw new Error(""{safeMessage}"");
}}

function onUnload() {{
    // 正常卸载
}}
";
            CreateJsFile(jsCode);

            var manifest = CreateTestManifest();
            var context = new PluginContext(manifest, _tempDir);

            // 加载脚本应该成功
            var loadScriptResult = context.LoadScript();

            // 调用 onLoad 应该返回 false（因为抛出异常），但不应崩溃
            var onLoadResult = context.CallOnLoad();

            // 上下文应该仍然可用
            var contextStillUsable = context.PluginId == "test-plugin";

            // 清理
            context.Dispose();

            return (loadScriptResult && !onLoadResult && contextStillUsable)
                .Label($"LoadScript: {loadScriptResult}, OnLoad: {onLoadResult}, Usable: {contextStillUsable}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 2: 异常隔离（运行时异常）**
        /// *对于任意*在函数调用中抛出异常的插件，InvokeFunction 应返回 false 但不崩溃
        /// **Validates: Requirements 1.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property RuntimeException_ShouldBeIsolated(PositiveInt funcIndex, NonEmptyString errorMsg)
        {
            // 使用预定义的有效函数名列表
            var validFuncNames = new[] { "testFunc", "myFunction", "doSomething", "processData", "handleEvent" };
            var funcName = validFuncNames[funcIndex.Get % validFuncNames.Length];

            var safeError = errorMsg.Get
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            // 创建一个会在指定函数中抛出异常的脚本
            var jsCode = $@"
function onLoad() {{}}
function onUnload() {{}}
function {funcName}() {{
    throw new Error(""{safeError}"");
}}
";
            CreateJsFile(jsCode);

            var manifest = CreateTestManifest();
            var context = new PluginContext(manifest, _tempDir);

            context.LoadScript();
            context.CallOnLoad();

            // 调用会抛出异常的函数
            var invokeResult = context.InvokeFunction(funcName);

            // 应该返回 false（异常被捕获）
            var exceptionCaught = !invokeResult;

            // 上下文应该仍然可用，可以继续调用其他函数
            var canCallOther = context.InvokeFunction("onUnload");

            context.Dispose();

            return (exceptionCaught && canCallOther)
                .Label($"Exception caught: {exceptionCaught}, Can call other: {canCallOther}");
        }

        #endregion


        #region Unit Tests

        /// <summary>
        /// 语法错误的脚本应该加载失败但不崩溃
        /// </summary>
        [Fact]
        public void SyntaxError_ShouldFailGracefully()
        {
            CreateJsFile("function onLoad( { invalid syntax }");

            var manifest = CreateTestManifest();
            var context = new PluginContext(manifest, _tempDir);

            var result = context.LoadScript();

            Assert.False(result);
            Assert.NotNull(context.LastError);
            // 错误消息应该包含有用的信息
            Assert.True(context.LastError.Contains("加载脚本失败") || context.LastError.Contains("Line"));

            context.Dispose();
        }

        /// <summary>
        /// 调用不存在的函数应该返回 true（跳过）
        /// </summary>
        [Fact]
        public void NonExistentFunction_ShouldReturnTrue()
        {
            CreateJsFile("function onLoad() {} function onUnload() {}");

            var manifest = CreateTestManifest();
            var context = new PluginContext(manifest, _tempDir);

            context.LoadScript();

            var result = context.InvokeFunction("nonExistentFunction");

            Assert.True(result);

            context.Dispose();
        }

        /// <summary>
        /// 无限循环应该超时而不是挂起
        /// </summary>
        [Fact]
        public void InfiniteLoop_ShouldTimeout()
        {
            CreateJsFile(@"
function onLoad() {}
function onUnload() {}
function infiniteLoop() {
    while(true) {}
}
");

            var manifest = CreateTestManifest();
            var context = new PluginContext(manifest, _tempDir);

            context.LoadScript();

            // 应该超时并返回 false
            var result = context.InvokeFunction("infiniteLoop");

            Assert.False(result);

            context.Dispose();
        }

        #endregion
    }
}
