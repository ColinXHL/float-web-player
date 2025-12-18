using System.Collections.Generic;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 取消订阅操作结果
    /// </summary>
    public class UnsubscribeResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息（失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 被取消订阅的插件列表（取消 Profile 订阅时使用）
        /// </summary>
        public List<string> UnsubscribedPlugins { get; set; } = new();

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static UnsubscribeResult Succeeded() => new() { Success = true };

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static UnsubscribeResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
    }
}
