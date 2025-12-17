using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// HTTP 网络请求 API
    /// 提供 HTTP GET/POST 请求功能
    /// 需要 "network" 权限
    /// </summary>
    public class HttpApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly HttpClient _httpClient;
        private const int DefaultTimeout = 30000; // 30 秒

        #endregion

        #region Constructor

        /// <summary>
        /// 创建 HTTP API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public HttpApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClient = new HttpClient();
        }

        #endregion

        #region HTTP Methods

        /// <summary>
        /// 发起 GET 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="options">请求选项（可选）：{ headers: {}, timeout: 30000 }</param>
        /// <returns>响应结果对象：{ success, status, data, error?, headers }</returns>
        public object Get(string url, object? options = null)
        {
            return ExecuteRequest(HttpMethod.Get, url, null, options);
        }

        /// <summary>
        /// 发起 POST 请求
        /// </summary>
        /// <param name="url">请求 URL</param>
        /// <param name="body">请求体（可选）</param>
        /// <param name="options">请求选项（可选）：{ headers: {}, timeout: 30000, contentType: "application/json" }</param>
        /// <returns>响应结果对象：{ success, status, data, error?, headers }</returns>
        public object Post(string url, object? body = null, object? options = null)
        {
            return ExecuteRequest(HttpMethod.Post, url, body, options);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            // 释放 HttpClient 资源
            try
            {
                _httpClient?.Dispose();
                Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "HttpApi: cleaned up");
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"HttpApi cleanup failed: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 执行 HTTP 请求
        /// </summary>
        private object ExecuteRequest(HttpMethod method, string url, object? body, object? options)
        {
            try
            {
                // 验证 URL
                if (string.IsNullOrWhiteSpace(url))
                {
                    return CreateErrorResponse("Invalid URL: URL cannot be empty");
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return CreateErrorResponse($"Invalid URL: {url}");
                }

                // 解析选项
                var requestOptions = ParseRequestOptions(options);

                // 创建请求
                using var request = new HttpRequestMessage(method, uri);

                // 添加请求体
                if (body != null && method == HttpMethod.Post)
                {
                    string bodyContent;
                    
                    // 如果 body 是字符串，直接使用；否则序列化为 JSON
                    if (body is string strBody)
                    {
                        bodyContent = strBody;
                    }
                    else
                    {
                        bodyContent = SerializeBody(body);
                    }
                    
                    request.Content = new StringContent(bodyContent, Encoding.UTF8, requestOptions.ContentType);
                }

                // 添加自定义头
                foreach (var header in requestOptions.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // 执行请求（同步等待）
                using var cts = new CancellationTokenSource(requestOptions.Timeout);
                var task = _httpClient.SendAsync(request, cts.Token);
                var response = task.GetAwaiter().GetResult();

                // 读取响应
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // 构建响应头字典
                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new
                {
                    success = true,
                    status = (int)response.StatusCode,
                    data = responseBody,
                    headers = responseHeaders
                };
            }
            catch (TaskCanceledException)
            {
                return CreateErrorResponse("Request timeout");
            }
            catch (HttpRequestException ex)
            {
                return CreateErrorResponse($"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"HttpApi request failed: {ex.Message}");
                return CreateErrorResponse($"Request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析请求选项
        /// </summary>
        private HttpRequestOptions ParseRequestOptions(object? options)
        {
            var result = new HttpRequestOptions();
            if (options == null)
                return result;

            var dict = ConvertToDictionary(options);
            if (dict == null)
                return result;

            // 解析 timeout
            if (dict.TryGetValue("timeout", out var timeout) && timeout != null)
            {
                try
                {
                    result.Timeout = Convert.ToInt32(timeout);
                    // 确保超时值在合理范围内
                    if (result.Timeout < 1000)
                        result.Timeout = 1000; // 最小 1 秒
                    if (result.Timeout > 300000)
                        result.Timeout = 300000; // 最大 5 分钟
                }
                catch
                {
                    // 使用默认值
                }
            }

            // 解析 contentType
            if (dict.TryGetValue("contentType", out var contentType) && contentType != null)
            {
                var ct = contentType.ToString();
                if (!string.IsNullOrWhiteSpace(ct))
                {
                    result.ContentType = ct;
                }
            }

            // 解析 headers
            if (dict.TryGetValue("headers", out var headers) && headers != null)
            {
                var headersDict = ConvertToDictionary(headers);
                if (headersDict != null)
                {
                    foreach (var kv in headersDict)
                    {
                        if (kv.Value != null)
                        {
                            result.Headers[kv.Key] = kv.Value.ToString() ?? string.Empty;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 序列化请求体为 JSON
        /// </summary>
        private static string SerializeBody(object body)
        {
            // 尝试将 Jint 对象转换为字典后序列化
            var dict = ConvertToDictionary(body);
            if (dict != null)
            {
                return System.Text.Json.JsonSerializer.Serialize(dict);
            }
            
            // 直接序列化
            return System.Text.Json.JsonSerializer.Serialize(body);
        }

        /// <summary>
        /// 创建错误响应对象
        /// </summary>
        private static object CreateErrorResponse(string error)
        {
            return new
            {
                success = false,
                status = 0,
                data = (string?)null,
                error = error,
                headers = (Dictionary<string, string>?)null
            };
        }

        /// <summary>
        /// 将对象转换为字典（支持 Jint 对象和匿名对象）
        /// </summary>
        private static Dictionary<string, object?>? ConvertToDictionary(object? obj)
        {
            if (obj == null)
                return null;

            // 如果已经是字典
            if (obj is Dictionary<string, object?> dict)
                return dict;

            if (obj is IDictionary<string, object> idict)
                return idict.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            // 尝试从 Jint ObjectInstance 获取属性
            var type = obj.GetType();
            if (type.FullName?.Contains("Jint") == true)
            {
                try
                {
                    var result = new Dictionary<string, object?>();
                    
                    // 使用反射获取 Jint 对象的属性
                    var getOwnPropertyKeysMethod = type.GetMethod("GetOwnPropertyKeys");
                    if (getOwnPropertyKeysMethod != null)
                    {
                        var keys = getOwnPropertyKeysMethod.Invoke(obj, new object[] { 0 }) as IEnumerable<object>;
                        if (keys != null)
                        {
                            var getMethod = type.GetMethod("Get", new[] { typeof(string) });
                            foreach (var key in keys)
                            {
                                var keyStr = key.ToString();
                                if (keyStr != null && getMethod != null)
                                {
                                    var value = getMethod.Invoke(obj, new object[] { keyStr });
                                    result[keyStr] = ConvertJintValue(value);
                                }
                            }
                        }
                    }
                    
                    return result.Count > 0 ? result : null;
                }
                catch
                {
                    return null;
                }
            }

            // 尝试从匿名对象获取属性
            try
            {
                var result = new Dictionary<string, object?>();
                foreach (var prop in type.GetProperties())
                {
                    result[prop.Name] = prop.GetValue(obj);
                }
                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 转换 Jint 值为 .NET 值
        /// </summary>
        private static object? ConvertJintValue(object? value)
        {
            if (value == null)
                return null;

            var type = value.GetType();
            
            // Jint JsNumber
            if (type.Name == "JsNumber" || type.Name == "JsValue")
            {
                var toObjectMethod = type.GetMethod("ToObject");
                if (toObjectMethod != null)
                {
                    return toObjectMethod.Invoke(value, null);
                }
            }

            // Jint JsString
            if (type.Name == "JsString")
            {
                return value.ToString();
            }

            return value;
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// HTTP 请求选项
        /// </summary>
        private class HttpRequestOptions
        {
            public Dictionary<string, string> Headers { get; set; } = new();
            public int Timeout { get; set; } = DefaultTimeout;
            public string ContentType { get; set; } = "application/json";
        }

        #endregion
    }
}
