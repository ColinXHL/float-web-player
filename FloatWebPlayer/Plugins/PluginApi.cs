using System;
using System.Collections.Generic;
using System.Diagnostics;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Plugins
{
    /// <summary>
    /// 插件 API 总入口
    /// 聚合所有子 API，并实现权限检查逻辑
    /// </summary>
    public class PluginApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly PluginManifest _manifest;
        private readonly PluginConfig _config;
        private readonly HashSet<string> _permissions;

        #endregion

        #region Properties

        /// <summary>
        /// 核心 API（日志、版本等）
        /// 无需权限
        /// </summary>
        public CoreApi Core { get; }

        /// <summary>
        /// 配置 API
        /// 无需权限
        /// </summary>
        public ConfigApi Config { get; }

        /// <summary>
        /// Profile 信息（只读）
        /// 无需权限
        /// </summary>
        public ProfileInfo Profile { get; }

        /// <summary>
        /// 语音识别 API
        /// 需要 "audio" 权限
        /// </summary>
        public SpeechApi? Speech => HasPermission("audio") ? _speechApi : null;
        private readonly SpeechApi? _speechApi;

        /// <summary>
        /// 覆盖层 API
        /// 需要 "overlay" 权限
        /// </summary>
        public OverlayApi? Overlay => HasPermission("overlay") ? _overlayApi : null;
        private readonly OverlayApi? _overlayApi;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建插件 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        /// <param name="config">插件配置</param>
        /// <param name="profileInfo">Profile 信息</param>
        public PluginApi(PluginContext context, PluginConfig config, ProfileInfo profileInfo)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _manifest = context.Manifest;
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 解析权限列表
            _permissions = new HashSet<string>(
                _manifest.Permissions ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase
            );

            Services.LogService.Instance.Debug("PluginApi", $"Plugin {context.PluginId} permissions: [{string.Join(", ", _permissions)}]");
            Services.LogService.Instance.Debug("PluginApi", $"HasPermission('overlay') = {HasPermission("overlay")}, HasPermission('audio') = {HasPermission("audio")}");

            // 初始化子 API
            Core = new CoreApi(context);
            Config = new ConfigApi(config);
            Profile = profileInfo ?? throw new ArgumentNullException(nameof(profileInfo));

            // 初始化需要权限的 API
            _speechApi = new SpeechApi(context);
            _overlayApi = new OverlayApi(context, Config);

            Services.LogService.Instance.Debug("PluginApi", $"_overlayApi is null = {_overlayApi == null}, Overlay property is null = {Overlay == null}");
        }

        #endregion

        #region Permission Methods

        /// <summary>
        /// 检查是否拥有指定权限
        /// </summary>
        /// <param name="permission">权限名称</param>
        /// <returns>是否拥有权限</returns>
        public bool HasPermission(string permission)
        {
            return _permissions.Contains(permission);
        }

        /// <summary>
        /// 要求指定权限，如果没有则抛出异常
        /// </summary>
        /// <param name="permission">权限名称</param>
        /// <exception cref="PermissionDeniedException">没有权限时抛出</exception>
        public void RequirePermission(string permission)
        {
            if (!HasPermission(permission))
            {
                throw new PermissionDeniedException(permission, _context.PluginId);
            }
        }

        /// <summary>
        /// 获取所有已授权的权限
        /// </summary>
        public IReadOnlyCollection<string> GetPermissions()
        {
            return _permissions;
        }

        #endregion

        #region Logging (Shortcut)

        /// <summary>
        /// 输出日志（快捷方法）
        /// </summary>
        /// <param name="message">日志内容</param>
        public void Log(object message)
        {
            Core.Log(message);
        }

        #endregion
    }

    /// <summary>
    /// Profile 信息（只读）
    /// </summary>
    public class ProfileInfo
    {
        /// <summary>
        /// Profile ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Profile 显示名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Profile 目录路径
        /// </summary>
        public string Directory { get; }

        public ProfileInfo(string id, string name, string directory)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Directory = directory ?? string.Empty;
        }
    }

    /// <summary>
    /// 权限拒绝异常
    /// </summary>
    public class PermissionDeniedException : Exception
    {
        /// <summary>
        /// 被拒绝的权限
        /// </summary>
        public string Permission { get; }

        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; }

        public PermissionDeniedException(string permission, string pluginId)
            : base($"插件 '{pluginId}' 没有 '{permission}' 权限")
        {
            Permission = permission;
            PluginId = pluginId;
        }
    }

}
