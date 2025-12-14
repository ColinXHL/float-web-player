using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;
using FloatWebPlayer.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// PluginHost 属性测试
    /// </summary>
    public class PluginHostTests : IDisposable
    {
        private readonly string _tempProfilesDir;
        private readonly string _testProfileId = "test-profile";

        public PluginHostTests()
        {
            _tempProfilesDir = Path.Combine(Path.GetTempPath(), $"plugin_host_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempProfilesDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempProfilesDir))
            {
                try
                {
                    Directory.Delete(_tempProfilesDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }

        /// <summary>
        /// 创建测试用的插件目录和文件
        /// </summary>
        private string CreateTestPlugin(string pluginId, string jsCode, bool enabled = true)
        {
            var pluginDir = Path.Combine(_tempProfilesDir, _testProfileId, "plugins", pluginId);
            Directory.CreateDirectory(pluginDir);

            // 创建 plugin.json
            var manifest = new
            {
                id = pluginId,
                name = $"Test Plugin {pluginId}",
                version = "1.0.0",
                main = "main.js"
            };
            File.WriteAllText(
                Path.Combine(pluginDir, "plugin.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })
            );

            // 创建 main.js
            File.WriteAllText(Path.Combine(pluginDir, "main.js"), jsCode);

            // 创建 config.json（如果需要禁用）
            if (!enabled)
            {
                var config = new { pluginId, enabled = false, settings = new { } };
                File.WriteAllText(
                    Path.Combine(pluginDir, "config.json"),
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
                );
            }

            return pluginDir;
        }


        #region Property 3: 生命周期回调

        /// <summary>
        /// **Feature: game-plugin-system, Property 3: 生命周期回调**
        /// *对于任意*有效插件，加载时 onLoad 函数应被调用，卸载时 onUnload 函数应被调用
        /// **Validates: Requirements 5.1, 5.2**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property LifecycleCallbacks_ShouldBeCalled(PositiveInt pluginCount)
        {
            // 限制插件数量在合理范围内
            var count = Math.Min(pluginCount.Get % 5 + 1, 5);

            // 为每次测试创建唯一的 profile ID
            var uniqueProfileId = $"profile-{Guid.NewGuid():N}";

            // 创建多个插件
            for (int i = 0; i < count; i++)
            {
                var jsCode = @"
function onLoad() {
    // onLoad called
}

function onUnload() {
    // onUnload called
}
";
                CreateTestPluginForProfile(uniqueProfileId, $"plugin-{i}", jsCode);
            }

            // 创建 PluginHost 实例（使用测试构造函数）
            var host = new TestablePluginHost(_tempProfilesDir);

            // 加载插件
            host.LoadPluginsForProfile(uniqueProfileId);

            // 验证所有插件都已加载
            var allLoaded = host.LoadedPlugins.Count == count;

            // 验证 onLoad 被调用（通过检查 IsLoaded 状态）
            var allOnLoadCalled = host.LoadedPlugins.All(p => p.IsLoaded);

            // 卸载所有插件
            host.UnloadAllPlugins();

            // 验证所有插件都已卸载
            var allUnloaded = host.LoadedPlugins.Count == 0;

            host.Dispose();

            return (allLoaded && allOnLoadCalled && allUnloaded)
                .Label($"Count: {count}, Loaded: {allLoaded} ({host.LoadedPlugins.Count}), OnLoad called: {allOnLoadCalled}, Unloaded: {allUnloaded}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 3: 生命周期回调（单插件）**
        /// *对于任意*有效插件，生命周期顺序应为：LoadScript -> onLoad -> onUnload -> Dispose
        /// **Validates: Requirements 5.1, 5.2**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property LifecycleOrder_ShouldBeCorrect(PositiveInt pluginIndex)
        {
            // 使用预定义的有效插件 ID 列表
            var validPluginIds = new[] { "test-plugin", "my-plugin", "sample", "demo", "example" };
            var pluginId = validPluginIds[pluginIndex.Get % validPluginIds.Length];

            var lifecycleLog = new System.Collections.Generic.List<string>();

            var jsCode = @"
function onLoad() {
    // onLoad called
}

function onUnload() {
    // onUnload called
}
";
            // 为每次测试创建唯一的 profile ID
            var uniqueProfileId = $"profile-{Guid.NewGuid():N}";
            var pluginDir = CreateTestPluginForProfile(uniqueProfileId, pluginId, jsCode);

            // 加载清单
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var loadResult = PluginManifest.LoadFromFile(manifestPath);
            
            if (!loadResult.IsSuccess)
                return false.ToProperty().Label("Failed to load manifest");

            var context = new PluginContext(loadResult.Manifest!, pluginDir);

            // 1. LoadScript
            var scriptLoaded = context.LoadScript();
            if (scriptLoaded) lifecycleLog.Add("LoadScript");

            // 2. onLoad
            var onLoadCalled = context.CallOnLoad();
            if (onLoadCalled && context.IsLoaded) lifecycleLog.Add("onLoad");

            // 3. onUnload
            var onUnloadCalled = context.CallOnUnload();
            if (onUnloadCalled && !context.IsLoaded) lifecycleLog.Add("onUnload");

            // 4. Dispose
            context.Dispose();
            lifecycleLog.Add("Dispose");

            var correctOrder = lifecycleLog.Count == 4 &&
                               lifecycleLog[0] == "LoadScript" &&
                               lifecycleLog[1] == "onLoad" &&
                               lifecycleLog[2] == "onUnload" &&
                               lifecycleLog[3] == "Dispose";

            return correctOrder
                .Label($"Lifecycle: {string.Join(" -> ", lifecycleLog)}");
        }

        /// <summary>
        /// 创建测试用的插件目录和文件（指定 profile）
        /// </summary>
        private string CreateTestPluginForProfile(string profileId, string pluginId, string jsCode, bool enabled = true)
        {
            var pluginDir = Path.Combine(_tempProfilesDir, profileId, "plugins", pluginId);
            Directory.CreateDirectory(pluginDir);

            // 创建 plugin.json
            var manifest = new
            {
                id = pluginId,
                name = $"Test Plugin {pluginId}",
                version = "1.0.0",
                main = "main.js"
            };
            File.WriteAllText(
                Path.Combine(pluginDir, "plugin.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })
            );

            // 创建 main.js
            File.WriteAllText(Path.Combine(pluginDir, "main.js"), jsCode);

            // 创建 config.json（如果需要禁用）
            if (!enabled)
            {
                var config = new { pluginId, enabled = false, settings = new { } };
                File.WriteAllText(
                    Path.Combine(pluginDir, "config.json"),
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
                );
            }

            return pluginDir;
        }

        #endregion


        #region Property 7: Profile 插件隔离

        /// <summary>
        /// **Feature: game-plugin-system, Property 7: Profile 插件隔离**
        /// *对于任意*两个不同的 Profile，切换 Profile 后应只加载目标 Profile 的插件，原 Profile 的插件应被卸载
        /// **Validates: Requirements 5.3, 5.4**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property ProfilePluginIsolation_ShouldUnloadOldAndLoadNew(PositiveInt profile1PluginCount, PositiveInt profile2PluginCount)
        {
            // 限制插件数量在合理范围内
            var count1 = Math.Min(profile1PluginCount.Get % 3 + 1, 3);
            var count2 = Math.Min(profile2PluginCount.Get % 3 + 1, 3);

            // 为每次测试创建唯一的 profile ID
            var profile1Id = $"profile1-{Guid.NewGuid():N}";
            var profile2Id = $"profile2-{Guid.NewGuid():N}";

            var jsCode = @"
function onLoad() {
    // onLoad called
}

function onUnload() {
    // onUnload called
}
";

            // 创建 Profile 1 的插件
            var profile1PluginIds = new List<string>();
            for (int i = 0; i < count1; i++)
            {
                var pluginId = $"p1-plugin-{i}";
                profile1PluginIds.Add(pluginId);
                CreateTestPluginForProfile(profile1Id, pluginId, jsCode);
            }

            // 创建 Profile 2 的插件
            var profile2PluginIds = new List<string>();
            for (int i = 0; i < count2; i++)
            {
                var pluginId = $"p2-plugin-{i}";
                profile2PluginIds.Add(pluginId);
                CreateTestPluginForProfile(profile2Id, pluginId, jsCode);
            }

            var host = new TestablePluginHost(_tempProfilesDir);

            // 加载 Profile 1
            host.LoadPluginsForProfile(profile1Id);
            var profile1Loaded = host.LoadedPlugins.Count == count1;
            var profile1PluginsCorrect = host.LoadedPlugins.All(p => profile1PluginIds.Contains(p.PluginId));

            // 切换到 Profile 2
            host.LoadPluginsForProfile(profile2Id);
            var profile2Loaded = host.LoadedPlugins.Count == count2;
            var profile2PluginsCorrect = host.LoadedPlugins.All(p => profile2PluginIds.Contains(p.PluginId));
            
            // 验证 Profile 1 的插件不再存在
            var profile1PluginsUnloaded = !host.LoadedPlugins.Any(p => profile1PluginIds.Contains(p.PluginId));

            host.Dispose();

            return (profile1Loaded && profile1PluginsCorrect && profile2Loaded && profile2PluginsCorrect && profile1PluginsUnloaded)
                .Label($"P1: {count1} plugins loaded={profile1Loaded}, correct={profile1PluginsCorrect}; " +
                       $"P2: {count2} plugins loaded={profile2Loaded}, correct={profile2PluginsCorrect}; " +
                       $"P1 unloaded={profile1PluginsUnloaded}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 7: Profile 插件隔离（无交叉）**
        /// *对于任意* Profile 切换，新 Profile 的插件列表不应包含旧 Profile 的任何插件
        /// **Validates: Requirements 5.3, 5.4**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property ProfilePluginIsolation_NoPluginCrossover(PositiveInt switchCount)
        {
            // 限制切换次数
            var switches = Math.Min(switchCount.Get % 5 + 2, 5);

            var jsCode = @"
function onLoad() {}
function onUnload() {}
";

            // 创建多个 Profile，每个有不同的插件
            var profiles = new List<(string Id, List<string> PluginIds)>();
            for (int i = 0; i < switches; i++)
            {
                var profileId = $"profile-{Guid.NewGuid():N}";
                var pluginIds = new List<string>();
                
                // 每个 Profile 有 1-2 个插件
                var pluginCount = (i % 2) + 1;
                for (int j = 0; j < pluginCount; j++)
                {
                    var pluginId = $"profile{i}-plugin{j}";
                    pluginIds.Add(pluginId);
                    CreateTestPluginForProfile(profileId, pluginId, jsCode);
                }
                
                profiles.Add((profileId, pluginIds));
            }

            var host = new TestablePluginHost(_tempProfilesDir);
            var allIsolated = true;

            // 依次切换 Profile 并验证隔离性
            for (int i = 0; i < profiles.Count; i++)
            {
                var (profileId, expectedPluginIds) = profiles[i];
                host.LoadPluginsForProfile(profileId);

                // 验证当前加载的插件只属于当前 Profile
                var currentPluginIds = host.LoadedPlugins.Select(p => p.PluginId).ToList();
                
                // 检查是否只包含当前 Profile 的插件
                var onlyCurrentProfilePlugins = currentPluginIds.All(id => expectedPluginIds.Contains(id));
                
                // 检查是否不包含其他 Profile 的插件
                var noOtherProfilePlugins = true;
                for (int j = 0; j < profiles.Count; j++)
                {
                    if (j != i)
                    {
                        var otherPluginIds = profiles[j].PluginIds;
                        if (currentPluginIds.Any(id => otherPluginIds.Contains(id)))
                        {
                            noOtherProfilePlugins = false;
                            break;
                        }
                    }
                }

                if (!onlyCurrentProfilePlugins || !noOtherProfilePlugins)
                {
                    allIsolated = false;
                    break;
                }
            }

            host.Dispose();

            return allIsolated.Label($"Tested {switches} profile switches, all isolated: {allIsolated}");
        }

        #endregion

        #region Property 1: 取消订阅后插件目录被删除

        /// <summary>
        /// **Feature: ui-improvements, Property 1: 取消订阅后插件目录被删除**
        /// *For any* 已加载的插件，当调用 UnsubscribePlugin 方法并成功返回后，
        /// 该插件的目录应不再存在于文件系统中，且该插件应从 LoadedPlugins 列表中移除。
        /// **Validates: Requirements 1.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property UnsubscribePlugin_ShouldDeleteDirectoryAndRemoveFromList(PositiveInt pluginIndex)
        {
            // 使用预定义的有效插件 ID 列表
            var validPluginIds = new[] { "test-plugin", "my-plugin", "sample", "demo", "example" };
            var pluginId = validPluginIds[pluginIndex.Get % validPluginIds.Length];

            // 为每次测试创建唯一的 profile ID
            var uniqueProfileId = $"profile-{Guid.NewGuid():N}";

            var jsCode = @"
function onLoad() {
    // onLoad called
}

function onUnload() {
    // onUnload called
}
";
            // 创建插件
            var pluginDir = CreateTestPluginForProfile(uniqueProfileId, pluginId, jsCode);

            // 验证插件目录存在
            var dirExistsBefore = Directory.Exists(pluginDir);

            // 创建 PluginHost 实例
            var host = new TestablePluginHost(_tempProfilesDir);

            // 加载插件
            host.LoadPluginsForProfile(uniqueProfileId);

            // 验证插件已加载
            var pluginLoadedBefore = host.LoadedPlugins.Any(p => p.PluginId == pluginId);
            var loadedCountBefore = host.LoadedPlugins.Count;

            // 取消订阅
            var result = host.UnsubscribePlugin(pluginId);

            // 验证结果
            var unsubscribeSuccess = result.Success;
            var dirExistsAfter = Directory.Exists(pluginDir);
            var pluginInListAfter = host.LoadedPlugins.Any(p => p.PluginId == pluginId);
            var loadedCountAfter = host.LoadedPlugins.Count;

            host.Dispose();

            // 属性：取消订阅成功后，目录应被删除，插件应从列表移除
            var property = dirExistsBefore && 
                           pluginLoadedBefore && 
                           unsubscribeSuccess && 
                           !dirExistsAfter && 
                           !pluginInListAfter &&
                           loadedCountAfter == loadedCountBefore - 1;

            return property.Label(
                $"PluginId: {pluginId}, " +
                $"DirBefore: {dirExistsBefore}, LoadedBefore: {pluginLoadedBefore}, " +
                $"Success: {unsubscribeSuccess}, DirAfter: {dirExistsAfter}, " +
                $"InListAfter: {pluginInListAfter}, CountBefore: {loadedCountBefore}, CountAfter: {loadedCountAfter}");
        }

        /// <summary>
        /// **Feature: ui-improvements, Property 1: 取消订阅后插件目录被删除（多插件场景）**
        /// *For any* 多个已加载的插件，取消订阅其中一个不应影响其他插件
        /// **Validates: Requirements 1.3**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property UnsubscribePlugin_ShouldNotAffectOtherPlugins(PositiveInt pluginCount, PositiveInt targetIndex)
        {
            // 限制插件数量在 2-5 之间
            var count = Math.Max(2, Math.Min(pluginCount.Get % 5 + 1, 5));
            var targetIdx = targetIndex.Get % count;

            // 为每次测试创建唯一的 profile ID
            var uniqueProfileId = $"profile-{Guid.NewGuid():N}";

            var jsCode = @"
function onLoad() {}
function onUnload() {}
";
            // 创建多个插件
            var pluginIds = new List<string>();
            var pluginDirs = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var pluginId = $"plugin-{i}";
                pluginIds.Add(pluginId);
                var dir = CreateTestPluginForProfile(uniqueProfileId, pluginId, jsCode);
                pluginDirs.Add(dir);
            }

            var targetPluginId = pluginIds[targetIdx];
            var targetPluginDir = pluginDirs[targetIdx];

            // 创建 PluginHost 实例
            var host = new TestablePluginHost(_tempProfilesDir);

            // 加载插件
            host.LoadPluginsForProfile(uniqueProfileId);

            // 验证所有插件都已加载
            var allLoadedBefore = host.LoadedPlugins.Count == count;

            // 取消订阅目标插件
            var result = host.UnsubscribePlugin(targetPluginId);

            // 验证结果
            var unsubscribeSuccess = result.Success;
            var targetDirDeleted = !Directory.Exists(targetPluginDir);
            var targetRemovedFromList = !host.LoadedPlugins.Any(p => p.PluginId == targetPluginId);
            
            // 验证其他插件不受影响
            var otherPluginsIntact = true;
            for (int i = 0; i < count; i++)
            {
                if (i != targetIdx)
                {
                    var otherId = pluginIds[i];
                    var otherDir = pluginDirs[i];
                    
                    if (!host.LoadedPlugins.Any(p => p.PluginId == otherId) || !Directory.Exists(otherDir))
                    {
                        otherPluginsIntact = false;
                        break;
                    }
                }
            }

            var expectedCountAfter = count - 1;
            var correctCountAfter = host.LoadedPlugins.Count == expectedCountAfter;

            host.Dispose();

            var property = allLoadedBefore && 
                           unsubscribeSuccess && 
                           targetDirDeleted && 
                           targetRemovedFromList && 
                           otherPluginsIntact &&
                           correctCountAfter;

            return property.Label(
                $"Count: {count}, Target: {targetIdx}, " +
                $"AllLoadedBefore: {allLoadedBefore}, Success: {unsubscribeSuccess}, " +
                $"TargetDeleted: {targetDirDeleted}, TargetRemoved: {targetRemovedFromList}, " +
                $"OthersIntact: {otherPluginsIntact}, CorrectCount: {correctCountAfter}");
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// 加载不存在的 Profile 应该不崩溃
        /// </summary>
        [Fact]
        public void LoadNonExistentProfile_ShouldNotCrash()
        {
            var host = new TestablePluginHost(_tempProfilesDir);

            // 不应该抛出异常
            host.LoadPluginsForProfile("non-existent-profile");

            Assert.Empty(host.LoadedPlugins);

            host.Dispose();
        }

        /// <summary>
        /// 禁用的插件不应该被加载
        /// </summary>
        [Fact]
        public void DisabledPlugin_ShouldNotBeLoaded()
        {
            var jsCode = "function onLoad() {} function onUnload() {}";
            CreateTestPlugin("disabled-plugin", jsCode, enabled: false);
            CreateTestPlugin("enabled-plugin", jsCode, enabled: true);

            var host = new TestablePluginHost(_tempProfilesDir);
            host.LoadPluginsForProfile(_testProfileId);

            // 只有启用的插件应该被加载
            Assert.Single(host.LoadedPlugins);
            Assert.Equal("enabled-plugin", host.LoadedPlugins[0].PluginId);

            host.Dispose();
        }

        /// <summary>
        /// SetPluginEnabled 应该正确更新状态
        /// </summary>
        [Fact]
        public void SetPluginEnabled_ShouldUpdateState()
        {
            var jsCode = "function onLoad() {} function onUnload() {}";
            CreateTestPlugin("test-plugin", jsCode);

            var host = new TestablePluginHost(_tempProfilesDir);
            host.LoadPluginsForProfile(_testProfileId);

            var plugin = host.GetPlugin("test-plugin");
            Assert.NotNull(plugin);
            Assert.True(plugin.IsEnabled);

            host.SetPluginEnabled("test-plugin", false);
            Assert.False(plugin.IsEnabled);

            host.SetPluginEnabled("test-plugin", true);
            Assert.True(plugin.IsEnabled);

            host.Dispose();
        }

        /// <summary>
        /// 切换 Profile 应该卸载旧插件并加载新插件
        /// </summary>
        [Fact]
        public void SwitchProfile_ShouldReloadPlugins()
        {
            // 创建两个 Profile 的插件
            var jsCode = "function onLoad() {} function onUnload() {}";
            
            // Profile 1
            var profile1Dir = Path.Combine(_tempProfilesDir, "profile1", "plugins", "plugin1");
            Directory.CreateDirectory(profile1Dir);
            File.WriteAllText(Path.Combine(profile1Dir, "plugin.json"), 
                JsonSerializer.Serialize(new { id = "plugin1", name = "Plugin 1", version = "1.0.0", main = "main.js" }));
            File.WriteAllText(Path.Combine(profile1Dir, "main.js"), jsCode);

            // Profile 2
            var profile2Dir = Path.Combine(_tempProfilesDir, "profile2", "plugins", "plugin2");
            Directory.CreateDirectory(profile2Dir);
            File.WriteAllText(Path.Combine(profile2Dir, "plugin.json"), 
                JsonSerializer.Serialize(new { id = "plugin2", name = "Plugin 2", version = "1.0.0", main = "main.js" }));
            File.WriteAllText(Path.Combine(profile2Dir, "main.js"), jsCode);

            var host = new TestablePluginHost(_tempProfilesDir);

            // 加载 Profile 1
            host.LoadPluginsForProfile("profile1");
            Assert.Single(host.LoadedPlugins);
            Assert.Equal("plugin1", host.LoadedPlugins[0].PluginId);

            // 切换到 Profile 2
            host.LoadPluginsForProfile("profile2");
            Assert.Single(host.LoadedPlugins);
            Assert.Equal("plugin2", host.LoadedPlugins[0].PluginId);

            host.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 可测试的 PluginHost（允许自定义 Profiles 目录）
    /// </summary>
    internal class TestablePluginHost : IDisposable
    {
        private readonly List<PluginContext> _loadedPlugins = new();
        private readonly Dictionary<string, PluginConfig> _pluginConfigs = new();
        private readonly string _profilesDirectory;
        private string? _currentProfileId;
        private bool _disposed;

        public IReadOnlyList<PluginContext> LoadedPlugins => _loadedPlugins.AsReadOnly();
        public string? CurrentProfileId => _currentProfileId;

        public TestablePluginHost(string profilesDirectory)
        {
            _profilesDirectory = profilesDirectory;
        }

        public void LoadPluginsForProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            if (_loadedPlugins.Count > 0)
            {
                UnloadAllPlugins();
            }

            _currentProfileId = profileId;

            var pluginsDir = Path.Combine(_profilesDirectory, profileId, "plugins");
            if (!Directory.Exists(pluginsDir))
                return;

            var pluginDirs = Directory.GetDirectories(pluginsDir);
            foreach (var pluginDir in pluginDirs)
            {
                LoadPlugin(pluginDir);
            }
        }

        public void UnloadAllPlugins()
        {
            foreach (var plugin in _loadedPlugins.ToList())
            {
                plugin.CallOnUnload();
                plugin.Dispose();
            }

            _loadedPlugins.Clear();
            _pluginConfigs.Clear();
            _currentProfileId = null;
        }

        public void SetPluginEnabled(string pluginId, bool enabled)
        {
            var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin == null)
                return;

            plugin.IsEnabled = enabled;

            if (_pluginConfigs.TryGetValue(pluginId, out var config))
            {
                config.Enabled = enabled;
            }
        }

        public PluginContext? GetPlugin(string pluginId)
        {
            return _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
        }

        /// <summary>
        /// 取消订阅插件（停止运行并删除插件目录）
        /// </summary>
        public UnsubscribeResult UnsubscribePlugin(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return UnsubscribeResult.Failed("插件 ID 不能为空");
            }

            var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin == null)
            {
                return UnsubscribeResult.Succeeded();
            }

            var pluginDir = plugin.PluginDirectory;

            try
            {
                plugin.CallOnUnload();
                plugin.Dispose();
                _loadedPlugins.Remove(plugin);
                _pluginConfigs.Remove(pluginId);

                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, recursive: true);
                }

                return UnsubscribeResult.Succeeded();
            }
            catch (Exception ex)
            {
                return UnsubscribeResult.Failed(ex.Message);
            }
        }

        private void LoadPlugin(string pluginDir)
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var loadResult = PluginManifest.LoadFromFile(manifestPath);
            if (!loadResult.IsSuccess)
                return;

            var manifest = loadResult.Manifest!;

            if (_loadedPlugins.Any(p => p.PluginId == manifest.Id))
                return;

            var configPath = Path.Combine(pluginDir, "config.json");
            var config = PluginConfig.LoadFromFile(configPath, manifest.Id!);
            config.ApplyDefaults(manifest.DefaultConfig);
            _pluginConfigs[manifest.Id!] = config;

            if (!config.Enabled)
                return;

            var context = new PluginContext(manifest, pluginDir)
            {
                IsEnabled = config.Enabled
            };

            if (!context.LoadScript())
            {
                context.Dispose();
                return;
            }

            context.CallOnLoad();
            _loadedPlugins.Add(context);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            UnloadAllPlugins();
            _disposed = true;
        }
    }
}
