using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// PluginApi 属性测试
    /// </summary>
    public class PluginApiTests : IDisposable
    {
        private readonly string _tempDir;

        public PluginApiTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"plugin_api_test_{Guid.NewGuid()}");
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
        private PluginManifest CreateTestManifest(string id = "test-plugin", List<string>? permissions = null)
        {
            return new PluginManifest
            {
                Id = id,
                Name = "Test Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = permissions
            };
        }

        /// <summary>
        /// 创建测试用的 JavaScript 文件
        /// </summary>
        private void CreateJsFile(string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, "main.js"), content);
        }

        /// <summary>
        /// 创建测试用的 PluginApi
        /// </summary>
        private PluginApi CreatePluginApi(List<string>? permissions = null)
        {
            var manifest = CreateTestManifest(permissions: permissions);
            CreateJsFile("function onLoad() {} function onUnload() {}");
            
            var context = new PluginContext(manifest, _tempDir);
            var config = new PluginConfig("test-plugin");
            var profileInfo = new ProfileInfo("test-profile", "Test Profile", _tempDir);

            return new PluginApi(context, config, profileInfo);
        }


        #region Property 1: 沙箱安全性

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性**
        /// *对于任意*插件代码，如果它尝试访问未在 permissions 中声明的 API，则该调用应抛出权限错误
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property UnauthorizedApiAccess_ShouldReturnNull(PositiveInt permIndex)
        {
            // 所有可能的权限
            var allPermissions = new[] { "audio", "overlay", "network", "storage" };
            
            // 随机选择一个权限作为"未授权"的权限
            var unauthorizedPermission = allPermissions[permIndex.Get % allPermissions.Length];
            
            // 创建一个不包含该权限的权限列表
            var grantedPermissions = new List<string>();
            foreach (var perm in allPermissions)
            {
                if (perm != unauthorizedPermission)
                {
                    grantedPermissions.Add(perm);
                }
            }

            var api = CreatePluginApi(grantedPermissions);

            // 验证未授权的权限
            var hasUnauthorizedPermission = api.HasPermission(unauthorizedPermission);
            
            // 验证已授权的权限
            var allGrantedPermissionsValid = grantedPermissions.TrueForAll(p => api.HasPermission(p));

            // 尝试访问未授权的 API 应该返回 null
            bool unauthorizedApiReturnsNull = unauthorizedPermission switch
            {
                "overlay" => api.Overlay == null,
                _ => true // audio 已移除，network 和 storage 尚未实现
            };

            return (!hasUnauthorizedPermission && allGrantedPermissionsValid && unauthorizedApiReturnsNull)
                .Label($"Unauthorized: {unauthorizedPermission}, HasPerm: {hasUnauthorizedPermission}, " +
                       $"GrantedValid: {allGrantedPermissionsValid}, ApiNull: {unauthorizedApiReturnsNull}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性（RequirePermission）**
        /// *对于任意*未授权的权限，调用 RequirePermission 应抛出 PermissionDeniedException
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property RequirePermission_ShouldThrowForUnauthorized(PositiveInt permIndex)
        {
            var allPermissions = new[] { "audio", "overlay", "network", "storage" };
            var unauthorizedPermission = allPermissions[permIndex.Get % allPermissions.Length];

            // 创建一个空权限列表的 API
            var api = CreatePluginApi(new List<string>());

            // 调用 RequirePermission 应该抛出异常
            var exceptionThrown = false;
            var correctExceptionType = false;
            var correctPermissionInException = false;

            try
            {
                api.RequirePermission(unauthorizedPermission);
            }
            catch (PermissionDeniedException ex)
            {
                exceptionThrown = true;
                correctExceptionType = true;
                correctPermissionInException = ex.Permission == unauthorizedPermission;
            }
            catch
            {
                exceptionThrown = true;
            }

            return (exceptionThrown && correctExceptionType && correctPermissionInException)
                .Label($"Permission: {unauthorizedPermission}, Thrown: {exceptionThrown}, " +
                       $"CorrectType: {correctExceptionType}, CorrectPerm: {correctPermissionInException}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 1: 沙箱安全性（授权访问）**
        /// *对于任意*已授权的权限，访问对应的 API 应该返回非 null 值
        /// **Validates: Requirements 1.2, 7.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property AuthorizedApiAccess_ShouldReturnNonNull(PositiveInt permIndex)
        {
            // 需要权限的 API (audio 已移除)
            var permissionApis = new[] { "overlay" };
            var authorizedPermission = permissionApis[permIndex.Get % permissionApis.Length];

            // 创建包含该权限的 API
            var api = CreatePluginApi(new List<string> { authorizedPermission });

            // 验证已授权的权限
            var hasPermission = api.HasPermission(authorizedPermission);

            // 访问已授权的 API 应该返回非 null
            bool authorizedApiReturnsNonNull = authorizedPermission switch
            {
                "overlay" => api.Overlay != null,
                _ => true
            };

            // RequirePermission 不应该抛出异常
            var noExceptionThrown = true;
            try
            {
                api.RequirePermission(authorizedPermission);
            }
            catch
            {
                noExceptionThrown = false;
            }

            return (hasPermission && authorizedApiReturnsNonNull && noExceptionThrown)
                .Label($"Permission: {authorizedPermission}, HasPerm: {hasPermission}, " +
                       $"ApiNonNull: {authorizedApiReturnsNonNull}, NoException: {noExceptionThrown}");
        }

        #endregion


        #region Property 1: 权限控制一致性 (新增 API)

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 1: 权限控制一致性**
        /// *对于任意*权限名称和对应的 API，当插件 manifest 声明该权限时，PluginApi 应暴露对应的 API 对象；
        /// 当未声明时，对应的 API 属性应返回 null。
        /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property PermissionControlConsistency_NewApis(PositiveInt permIndex)
        {
            // 新增的需要权限的 API 及其对应权限
            var permissionApiPairs = new[]
            {
                ("player", new Func<PluginApi, object?>(api => api.Player)),
                ("window", new Func<PluginApi, object?>(api => api.Window)),
                ("storage", new Func<PluginApi, object?>(api => api.Storage)),
                ("network", new Func<PluginApi, object?>(api => api.Http)),
                ("events", new Func<PluginApi, object?>(api => api.Event))
            };

            // 选择一个权限进行测试
            var (permission, apiGetter) = permissionApiPairs[permIndex.Get % permissionApiPairs.Length];

            // 测试 1: 有权限时应该返回非 null
            var apiWithPermission = CreatePluginApi(new List<string> { permission });
            var hasPermissionResult = apiWithPermission.HasPermission(permission);
            var apiWithPermissionNotNull = apiGetter(apiWithPermission) != null;

            // 测试 2: 无权限时应该返回 null
            var apiWithoutPermission = CreatePluginApi(new List<string>());
            var noPermissionResult = !apiWithoutPermission.HasPermission(permission);
            var apiWithoutPermissionIsNull = apiGetter(apiWithoutPermission) == null;

            // 测试 3: RequirePermission 在有权限时不抛异常
            var requirePermissionNoThrow = true;
            try
            {
                apiWithPermission.RequirePermission(permission);
            }
            catch
            {
                requirePermissionNoThrow = false;
            }

            // 测试 4: RequirePermission 在无权限时抛出 PermissionDeniedException
            var requirePermissionThrows = false;
            var correctExceptionType = false;
            try
            {
                apiWithoutPermission.RequirePermission(permission);
            }
            catch (PermissionDeniedException ex)
            {
                requirePermissionThrows = true;
                correctExceptionType = ex.Permission == permission;
            }
            catch
            {
                requirePermissionThrows = true;
            }

            return (hasPermissionResult && apiWithPermissionNotNull && 
                    noPermissionResult && apiWithoutPermissionIsNull &&
                    requirePermissionNoThrow && requirePermissionThrows && correctExceptionType)
                .Label($"Permission: {permission}, " +
                       $"HasPerm: {hasPermissionResult}, ApiNotNull: {apiWithPermissionNotNull}, " +
                       $"NoPerm: {noPermissionResult}, ApiNull: {apiWithoutPermissionIsNull}, " +
                       $"NoThrow: {requirePermissionNoThrow}, Throws: {requirePermissionThrows}, CorrectEx: {correctExceptionType}");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 1: 权限控制一致性（多权限组合）**
        /// *对于任意*权限子集，只有声明的权限对应的 API 应该可用，未声明的应该返回 null。
        /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property PermissionControlConsistency_MultiplePermissions(PositiveInt seed)
        {
            // 所有新增的权限
            var allNewPermissions = new[] { "player", "window", "storage", "network", "events" };
            
            // 根据 seed 生成一个权限子集
            var grantedPermissions = new List<string>();
            var seedValue = seed.Get;
            for (int i = 0; i < allNewPermissions.Length; i++)
            {
                if ((seedValue & (1 << i)) != 0)
                {
                    grantedPermissions.Add(allNewPermissions[i]);
                }
            }

            var api = CreatePluginApi(grantedPermissions);

            // 验证每个权限的 API 可用性
            var playerCorrect = (api.Player != null) == grantedPermissions.Contains("player");
            var windowCorrect = (api.Window != null) == grantedPermissions.Contains("window");
            var storageCorrect = (api.Storage != null) == grantedPermissions.Contains("storage");
            var httpCorrect = (api.Http != null) == grantedPermissions.Contains("network");
            var eventCorrect = (api.Event != null) == grantedPermissions.Contains("events");

            // 验证 HasPermission 方法
            var hasPermissionCorrect = allNewPermissions.All(p => 
                api.HasPermission(p) == grantedPermissions.Contains(p));

            return (playerCorrect && windowCorrect && storageCorrect && httpCorrect && eventCorrect && hasPermissionCorrect)
                .Label($"Granted: [{string.Join(",", grantedPermissions)}], " +
                       $"Player: {playerCorrect}, Window: {windowCorrect}, Storage: {storageCorrect}, " +
                       $"Http: {httpCorrect}, Event: {eventCorrect}, HasPerm: {hasPermissionCorrect}");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 1: 权限控制一致性（无权限 API 始终可用）**
        /// *对于任意*权限配置，Core、Config、Profile API 应该始终可用（无需权限）。
        /// **Validates: Requirements 7.6**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property NoPermissionApis_AlwaysAvailable(PositiveInt seed)
        {
            // 随机生成权限列表
            var allPermissions = new[] { "player", "window", "storage", "network", "events", "audio", "overlay", "subtitle" };
            var grantedPermissions = new List<string>();
            var seedValue = seed.Get;
            for (int i = 0; i < allPermissions.Length; i++)
            {
                if ((seedValue & (1 << i)) != 0)
                {
                    grantedPermissions.Add(allPermissions[i]);
                }
            }

            var api = CreatePluginApi(grantedPermissions);

            // 无需权限的 API 应该始终可用
            var coreAvailable = api.Core != null;
            var configAvailable = api.Config != null;
            var profileAvailable = api.Profile != null;

            return (coreAvailable && configAvailable && profileAvailable)
                .Label($"Granted: [{string.Join(",", grantedPermissions)}], " +
                       $"Core: {coreAvailable}, Config: {configAvailable}, Profile: {profileAvailable}");
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// 无权限的插件应该无法访问 Overlay API
        /// </summary>
        [Fact]
        public void NoPermission_ShouldNotAccessOverlayApi()
        {
            var api = CreatePluginApi(new List<string>());

            Assert.Null(api.Overlay);
            Assert.False(api.HasPermission("overlay"));
        }

        /// <summary>
        /// 有 overlay 权限的插件应该能访问 Overlay API
        /// </summary>
        [Fact]
        public void WithOverlayPermission_ShouldAccessOverlayApi()
        {
            var api = CreatePluginApi(new List<string> { "overlay" });

            Assert.NotNull(api.Overlay);
            Assert.True(api.HasPermission("overlay"));
        }

        /// <summary>
        /// Core API 应该始终可用（无需权限）
        /// </summary>
        [Fact]
        public void CoreApi_ShouldAlwaysBeAvailable()
        {
            var api = CreatePluginApi(new List<string>());

            Assert.NotNull(api.Core);
            Assert.NotNull(api.Config);
            Assert.NotNull(api.Profile);
        }

        /// <summary>
        /// Log 方法应该不抛出异常
        /// </summary>
        [Fact]
        public void Log_ShouldNotThrow()
        {
            var api = CreatePluginApi(new List<string>());

            // 不应该抛出异常
            api.Log("Test message");
            api.Core.Log("Test message");
            api.Core.Warn("Warning message");
            api.Core.Error("Error message");
        }

        /// <summary>
        /// Profile 信息应该正确
        /// </summary>
        [Fact]
        public void ProfileInfo_ShouldBeCorrect()
        {
            var manifest = CreateTestManifest();
            CreateJsFile("function onLoad() {} function onUnload() {}");
            
            var context = new PluginContext(manifest, _tempDir);
            var config = new PluginConfig("test-plugin");
            var profileInfo = new ProfileInfo("my-profile", "My Profile", "/path/to/profile");

            var api = new PluginApi(context, config, profileInfo);

            Assert.Equal("my-profile", api.Profile.Id);
            Assert.Equal("My Profile", api.Profile.Name);
            Assert.Equal("/path/to/profile", api.Profile.Directory);
        }

        /// <summary>
        /// 权限检查应该不区分大小写
        /// </summary>
        [Fact]
        public void PermissionCheck_ShouldBeCaseInsensitive()
        {
            var api = CreatePluginApi(new List<string> { "AUDIO", "Overlay" });

            Assert.True(api.HasPermission("audio"));
            Assert.True(api.HasPermission("AUDIO"));
            Assert.True(api.HasPermission("Audio"));
            Assert.True(api.HasPermission("overlay"));
            Assert.True(api.HasPermission("OVERLAY"));
        }

        #endregion
    }
}
