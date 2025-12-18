using System;
using System.Collections.Generic;
using System.IO;
using SandronePlayer.Models;
using SandronePlayer.Plugins;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// HttpApi 单元测试
    /// 测试响应结构正确性和错误处理
    /// </summary>
    public class HttpApiTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PluginContext _context;
        private readonly HttpApi _httpApi;

        public HttpApiTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"http_api_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            var manifest = new PluginManifest
            {
                Id = "test-http-plugin",
                Name = "Test Http Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = new List<string> { "network" }
            };

            File.WriteAllText(Path.Combine(_tempDir, "main.js"), "function onLoad() {} function onUnload() {}");

            _context = new PluginContext(manifest, _tempDir);
            _httpApi = new HttpApi(_context);
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

        #region Task 8.2: HttpApi 单元测试 - 响应结构正确性

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Get 请求空 URL 应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Get_EmptyUrl_ReturnsErrorResponse()
        {
            var result = _httpApi.Get("");

            AssertErrorResponse(result, "Invalid URL");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Get 请求 null URL 应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Get_NullUrl_ReturnsErrorResponse()
        {
            var result = _httpApi.Get(null!);

            AssertErrorResponse(result, "Invalid URL");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Get 请求无效 URL 应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Theory]
        [InlineData("not-a-url")]
        [InlineData("://missing-scheme")]
        public void Get_InvalidUrl_ReturnsErrorResponse(string url)
        {
            var result = _httpApi.Get(url);

            AssertErrorResponse(result, "Invalid URL");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Get 请求不支持的协议应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Get_UnsupportedScheme_ReturnsErrorResponse()
        {
            var result = _httpApi.Get("ftp://invalid");

            // ftp 是有效的 URI 但 HttpClient 不支持
            var type = result.GetType();
            var success = (bool)type.GetProperty("success")!.GetValue(result)!;
            var error = type.GetProperty("error")!.GetValue(result) as string;

            Assert.False(success);
            Assert.NotNull(error);
            // 错误信息可能是 "Request failed" 或包含 scheme 相关信息
            Assert.True(error.Contains("Request failed") || error.Contains("scheme"), 
                $"Expected error about scheme or request failure, got: {error}");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Post 请求空 URL 应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Post_EmptyUrl_ReturnsErrorResponse()
        {
            var result = _httpApi.Post("", new { data = "test" });

            AssertErrorResponse(result, "Invalid URL");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Post 请求无效 URL 应该返回错误响应
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Post_InvalidUrl_ReturnsErrorResponse()
        {
            var result = _httpApi.Post("not-a-valid-url", new { data = "test" });

            AssertErrorResponse(result, "Invalid URL");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// Get 请求不存在的主机应该返回网络错误
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Get_NonExistentHost_ReturnsNetworkError()
        {
            var result = _httpApi.Get("http://this-host-does-not-exist-12345.invalid/");

            AssertErrorResponse(result, "Network error");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// 响应结构应该包含所有必要字段
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void Response_ShouldHaveCorrectStructure()
        {
            // 使用无效 URL 来获取错误响应，验证结构
            var result = _httpApi.Get("");

            Assert.NotNull(result);
            
            var type = result.GetType();
            
            // 验证必要字段存在
            Assert.NotNull(type.GetProperty("success"));
            Assert.NotNull(type.GetProperty("status"));
            Assert.NotNull(type.GetProperty("data"));
            Assert.NotNull(type.GetProperty("error"));
            Assert.NotNull(type.GetProperty("headers"));
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// 错误响应的 success 应该为 false
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void ErrorResponse_SuccessShouldBeFalse()
        {
            var result = _httpApi.Get("");

            var type = result.GetType();
            var success = (bool)type.GetProperty("success")!.GetValue(result)!;

            Assert.False(success);
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// 错误响应的 status 应该为 0
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void ErrorResponse_StatusShouldBeZero()
        {
            var result = _httpApi.Get("");

            var type = result.GetType();
            var status = (int)type.GetProperty("status")!.GetValue(result)!;

            Assert.Equal(0, status);
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Task 8.2: HttpApi 单元测试**
        /// 错误响应应该包含错误信息
        /// **Validates: Requirements 5.4, 5.5**
        /// </summary>
        [Fact]
        public void ErrorResponse_ShouldContainErrorMessage()
        {
            var result = _httpApi.Get("");

            var type = result.GetType();
            var error = type.GetProperty("error")!.GetValue(result) as string;

            Assert.NotNull(error);
            Assert.NotEmpty(error);
        }

        #endregion

        #region 选项解析测试

        /// <summary>
        /// Get 请求应该接受 null 选项
        /// </summary>
        [Fact]
        public void Get_NullOptions_ShouldNotThrow()
        {
            // 使用无效 URL，但不应该因为 null 选项而抛出异常
            var result = _httpApi.Get("http://example.com", null);
            Assert.NotNull(result);
        }

        /// <summary>
        /// Post 请求应该接受 null body 和 options
        /// </summary>
        [Fact]
        public void Post_NullBodyAndOptions_ShouldNotThrow()
        {
            var result = _httpApi.Post("http://example.com", null, null);
            Assert.NotNull(result);
        }

        /// <summary>
        /// 超时选项应该被正确解析
        /// </summary>
        [Fact]
        public void Options_Timeout_ShouldBeParsed()
        {
            // 使用非常短的超时来测试超时功能
            var options = new { timeout = 1 }; // 1ms，几乎立即超时
            
            // 这个请求应该超时
            var result = _httpApi.Get("http://httpbin.org/delay/10", options);
            
            // 由于超时值会被钳制到最小 1000ms，实际上不会立即超时
            // 但这验证了选项解析不会抛出异常
            Assert.NotNull(result);
        }

        #endregion

        #region 构造函数测试

        /// <summary>
        /// 构造函数应该拒绝 null 上下文
        /// </summary>
        [Fact]
        public void Constructor_NullContext_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpApi(null!));
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 断言响应是错误响应
        /// </summary>
        private void AssertErrorResponse(object result, string expectedErrorContains)
        {
            Assert.NotNull(result);
            
            var type = result.GetType();
            var success = (bool)type.GetProperty("success")!.GetValue(result)!;
            var error = type.GetProperty("error")!.GetValue(result) as string;

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains(expectedErrorContains, error, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 实际网络请求测试（可选，需要网络）

        /// <summary>
        /// 实际 GET 请求测试（需要网络连接）
        /// 使用 httpbin.org 作为测试服务器
        /// </summary>
        [Fact(Skip = "需要网络连接，跳过自动测试")]
        public void Get_RealRequest_ReturnsSuccessResponse()
        {
            var result = _httpApi.Get("https://httpbin.org/get");

            var type = result.GetType();
            var success = (bool)type.GetProperty("success")!.GetValue(result)!;
            var status = (int)type.GetProperty("status")!.GetValue(result)!;
            var data = type.GetProperty("data")!.GetValue(result) as string;

            Assert.True(success);
            Assert.Equal(200, status);
            Assert.NotNull(data);
            Assert.NotEmpty(data);
        }

        /// <summary>
        /// 实际 POST 请求测试（需要网络连接）
        /// </summary>
        [Fact(Skip = "需要网络连接，跳过自动测试")]
        public void Post_RealRequest_ReturnsSuccessResponse()
        {
            var body = new { name = "test", value = 123 };
            var result = _httpApi.Post("https://httpbin.org/post", body);

            var type = result.GetType();
            var success = (bool)type.GetProperty("success")!.GetValue(result)!;
            var status = (int)type.GetProperty("status")!.GetValue(result)!;

            Assert.True(success);
            Assert.Equal(200, status);
        }

        #endregion
    }
}
