using System;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 插件更新操作结果
    /// 用于表示插件更新操作的执行结果
    /// </summary>
    public class UpdateResult
    {
        /// <summary>
        /// 更新是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息（失败时有值）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 更新前的版本号
        /// </summary>
        public string? OldVersion { get; set; }

        /// <summary>
        /// 更新后的版本号
        /// </summary>
        public string? NewVersion { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="oldVersion">更新前版本</param>
        /// <param name="newVersion">更新后版本</param>
        /// <returns>成功的更新结果</returns>
        public static UpdateResult Success(string oldVersion, string newVersion)
        {
            return new UpdateResult
            {
                IsSuccess = true,
                OldVersion = oldVersion,
                NewVersion = newVersion
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>失败的更新结果</returns>
        public static UpdateResult Failed(string errorMessage)
        {
            return new UpdateResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 创建无可用更新的结果
        /// </summary>
        /// <returns>无更新的结果</returns>
        public static UpdateResult NoUpdateAvailable()
        {
            return new UpdateResult
            {
                IsSuccess = false,
                ErrorMessage = "没有可用的更新"
            };
        }
    }
}
