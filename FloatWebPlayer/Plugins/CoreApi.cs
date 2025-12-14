using System;

namespace FloatWebPlayer.Plugins
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

        #endregion

        #region Properties

        /// <summary>
        /// 主程序版本
        /// </summary>
        public string Version => AppConstants.Version;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建核心 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public CoreApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion

        #region Methods

        /// <summary>
        /// 输出日志信息到主程序日志系统
        /// </summary>
        /// <param name="message">日志内容</param>
        public void Log(object message)
        {
            var text = message?.ToString() ?? "null";
            Services.LogService.Instance.Info($"Plugin:{_context.PluginId}", text);
        }

        /// <summary>
        /// 输出警告日志
        /// </summary>
        /// <param name="message">警告内容</param>
        public void Warn(object message)
        {
            var text = message?.ToString() ?? "null";
            Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", text);
        }

        /// <summary>
        /// 输出错误日志
        /// </summary>
        /// <param name="message">错误内容</param>
        public void Error(object message)
        {
            var text = message?.ToString() ?? "null";
            Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", text);
        }

        #endregion
    }
}
