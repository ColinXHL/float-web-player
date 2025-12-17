using System;
using System.Collections.Generic;
using System.Diagnostics;
using FloatWebPlayer.Models;
using FloatWebPlayer.Views;

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

        // 用于延迟设置的窗口引用
        private static Func<PlayerWindow?>? _globalWindowGetter;

        #endregion

        #region Static Methods

        /// <summary>
        /// 设置全局 PlayerWindow 获取器
        /// 应在应用启动时调用
        /// </summary>
        /// <param name="windowGetter">获取 PlayerWindow 的委托</param>
        public static void SetGlobalWindowGetter(Func<PlayerWindow?> windowGetter)
        {
            _globalWindowGetter = windowGetter;
        }

        #endregion

        #region Properties - No Permission Required

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

        #endregion

        #region Properties - Existing APIs (Permission Required)

        /// <summary>
        /// 覆盖层 API
        /// 需要 "overlay" 权限
        /// </summary>
        public OverlayApi? Overlay => HasPermission(PluginPermissions.Overlay) ? _overlayApi : null;
        private readonly OverlayApi? _overlayApi;

        /// <summary>
        /// 字幕 API
        /// 需要 "subtitle" 权限
        /// </summary>
        public SubtitleApi? Subtitle => HasPermission(PluginPermissions.Subtitle) ? _subtitleApi : null;
        private readonly SubtitleApi? _subtitleApi;

        #endregion

        #region Properties - New APIs (Permission Required)

        /// <summary>
        /// 播放器控制 API
        /// 需要 "player" 权限
        /// </summary>
        public PlayerApi? Player => HasPermission(PluginPermissions.Player) ? _playerApi : null;
        private readonly PlayerApi? _playerApi;

        /// <summary>
        /// 窗口控制 API
        /// 需要 "window" 权限
        /// </summary>
        public WindowApi? Window => HasPermission(PluginPermissions.Window) ? _windowApi : null;
        private readonly WindowApi? _windowApi;

        /// <summary>
        /// 数据存储 API
        /// 需要 "storage" 权限
        /// </summary>
        public StorageApi? Storage => HasPermission(PluginPermissions.Storage) ? _storageApi : null;
        private readonly StorageApi? _storageApi;

        /// <summary>
        /// HTTP 网络请求 API
        /// 需要 "network" 权限
        /// </summary>
        public HttpApi? Http => HasPermission(PluginPermissions.Network) ? _httpApi : null;
        private readonly HttpApi? _httpApi;

        /// <summary>
        /// 事件系统 API
        /// 需要 "events" 权限
        /// </summary>
        public EventApi? Event => HasPermission(PluginPermissions.Events) ? _eventApi : null;
        private readonly EventApi? _eventApi;

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
            Services.LogService.Instance.Debug("PluginApi", $"HasPermission('overlay') = {HasPermission(PluginPermissions.Overlay)}, HasPermission('audio') = {HasPermission(PluginPermissions.Audio)}");

            // 初始化无需权限的 API
            Core = new CoreApi(context);
            Config = new ConfigApi(config);
            Profile = profileInfo ?? throw new ArgumentNullException(nameof(profileInfo));

            // 初始化现有需要权限的 API
            _overlayApi = new OverlayApi(context, Config);
            _subtitleApi = new SubtitleApi(context);

            // 初始化新增的需要权限的 API
            _playerApi = _globalWindowGetter != null 
                ? new PlayerApi(context, _globalWindowGetter) 
                : new PlayerApi(context);
            _windowApi = _globalWindowGetter != null 
                ? new WindowApi(context, _globalWindowGetter) 
                : new WindowApi(context);
            _storageApi = new StorageApi(context);
            _httpApi = new HttpApi(context);
            _eventApi = new EventApi(context);

            // 将 EventApi 引用传递给需要触发事件的 API
            _windowApi.SetEventApi(_eventApi);
            _playerApi.SetEventApi(_eventApi);

            Services.LogService.Instance.Debug("PluginApi", $"_overlayApi is null = {_overlayApi == null}, Overlay property is null = {Overlay == null}");
            Services.LogService.Instance.Debug("PluginApi", $"_subtitleApi is null = {_subtitleApi == null}, Subtitle property is null = {Subtitle == null}");
            Services.LogService.Instance.Debug("PluginApi", $"New APIs initialized: Player={Player != null}, Window={Window != null}, Storage={Storage != null}, Http={Http != null}, Event={Event != null}");
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

        #region Cleanup

        private bool _isCleanedUp = false;

        /// <summary>
        /// 清理所有 API 资源（插件卸载时调用）
        /// 此方法是幂等的，可以安全地多次调用
        /// 只清理已初始化的 API（即不为 null 的 API）
        /// </summary>
        public void Cleanup()
        {
            // 确保幂等性：如果已经清理过，直接返回
            if (_isCleanedUp)
            {
                Services.LogService.Instance.Debug("PluginApi", $"Plugin {_context.PluginId} already cleaned up, skipping");
                return;
            }

            Services.LogService.Instance.Debug("PluginApi", $"Cleaning up plugin {_context.PluginId}");

            // 清理已初始化的子 API，使用 try-catch 确保一个 API 失败不影响其他 API 的清理
            // 只清理那些实际被初始化的 API（即不为 null 的 API）
            if (_overlayApi != null)
                TryCleanupApi("OverlayApi", () => _overlayApi.Cleanup());
            if (_subtitleApi != null)
                TryCleanupApi("SubtitleApi", () => _subtitleApi.Cleanup());
            if (_playerApi != null)
                TryCleanupApi("PlayerApi", () => _playerApi.Cleanup());
            if (_windowApi != null)
                TryCleanupApi("WindowApi", () => _windowApi.Cleanup());
            if (_storageApi != null)
                TryCleanupApi("StorageApi", () => _storageApi.Cleanup());
            if (_httpApi != null)
                TryCleanupApi("HttpApi", () => _httpApi.Cleanup());
            if (_eventApi != null)
                TryCleanupApi("EventApi", () => _eventApi.Cleanup());

            _isCleanedUp = true;
            Services.LogService.Instance.Debug("PluginApi", $"Plugin {_context.PluginId} cleanup completed");
        }

        /// <summary>
        /// 尝试清理单个 API，捕获并记录异常
        /// </summary>
        /// <param name="apiName">API 名称（用于日志）</param>
        /// <param name="cleanupAction">清理操作</param>
        private void TryCleanupApi(string apiName, Action cleanupAction)
        {
            try
            {
                cleanupAction?.Invoke();
                Services.LogService.Instance.Debug("PluginApi", $"  - {apiName} cleaned up successfully");
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error("PluginApi", $"  - Failed to cleanup {apiName}: {ex.Message}");
                // 继续清理其他 API，不抛出异常
            }
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
