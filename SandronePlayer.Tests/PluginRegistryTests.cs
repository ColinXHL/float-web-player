using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SandronePlayer.Services;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// PluginRegistry 单元测试
    /// </summary>
    public class PluginRegistryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _pluginsDir;

        public PluginRegistryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"plugin_registry_test_{Guid.NewGuid()}");
            _pluginsDir = Path.Combine(_tempDir, "Plugins");
            Directory.CreateDirectory(_pluginsDir);
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
        /// 创建测试用的 registry.json 文件
        /// </summary>
        private void CreateRegistryFile(object data)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Path.Combine(_pluginsDir, "registry.json"), json);
        }


        /// <summary>
        /// 创建标准测试数据
        /// </summary>
        private object CreateStandardRegistryData()
        {
            return new
            {
                version = 1,
                plugins = new[]
                {
                    new
                    {
                        id = "genshin-direction-marker",
                        name = "原神方向标记",
                        version = "2.0.0",
                        author = "ColinXHL",
                        description = "识别攻略视频中的方位词，在小地图上显示方向标记",
                        tags = new[] { "原神", "genshin", "方向" },
                        permissions = new[] { "subtitle", "overlay" },
                        profiles = new[] { "genshin" }
                    },
                    new
                    {
                        id = "subtitle-highlight",
                        name = "字幕高亮",
                        version = "1.0.0",
                        author = "Test",
                        description = "高亮显示字幕中的关键词",
                        tags = new[] { "字幕", "高亮" },
                        permissions = new[] { "subtitle" },
                        profiles = Array.Empty<string>()
                    }
                }
            };
        }

        #region GetAllPlugins Tests

        /// <summary>
        /// GetAllPlugins 应该返回所有内置插件
        /// </summary>
        [Fact]
        public void GetAllPlugins_ShouldReturnAllPlugins_WhenRegistryExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugins = registry.GetAllPlugins();

            // Assert
            Assert.Equal(2, plugins.Count);
            Assert.Contains(plugins, p => p.Id == "genshin-direction-marker");
            Assert.Contains(plugins, p => p.Id == "subtitle-highlight");
        }


        /// <summary>
        /// GetAllPlugins 应该返回空列表当索引文件不存在时
        /// </summary>
        [Fact]
        public void GetAllPlugins_ShouldReturnEmptyList_WhenRegistryNotExists()
        {
            // Arrange - 不创建 registry.json
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugins = registry.GetAllPlugins();

            // Assert
            Assert.Empty(plugins);
        }

        /// <summary>
        /// GetAllPlugins 应该返回副本而非原始列表
        /// </summary>
        [Fact]
        public void GetAllPlugins_ShouldReturnCopy_NotOriginalList()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugins1 = registry.GetAllPlugins();
            var plugins2 = registry.GetAllPlugins();

            // Assert - 修改一个列表不应影响另一个
            plugins1.Clear();
            Assert.Equal(2, plugins2.Count);
        }

        #endregion

        #region GetPlugin Tests

        /// <summary>
        /// GetPlugin 应该返回正确的插件
        /// </summary>
        [Fact]
        public void GetPlugin_ShouldReturnPlugin_WhenExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugin = registry.GetPlugin("genshin-direction-marker");

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal("genshin-direction-marker", plugin!.Id);
            Assert.Equal("原神方向标记", plugin.Name);
            Assert.Equal("2.0.0", plugin.Version);
            Assert.Equal("ColinXHL", plugin.Author);
            Assert.Contains("subtitle", plugin.Permissions);
            Assert.Contains("overlay", plugin.Permissions);
            Assert.Contains("genshin", plugin.Profiles);
        }


        /// <summary>
        /// GetPlugin 应该返回 null 当插件不存在时
        /// </summary>
        [Fact]
        public void GetPlugin_ShouldReturnNull_WhenNotExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugin = registry.GetPlugin("non-existent");

            // Assert
            Assert.Null(plugin);
        }

        /// <summary>
        /// GetPlugin 应该返回 null 当 ID 为空时
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetPlugin_ShouldReturnNull_WhenIdIsNullOrEmpty(string? pluginId)
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugin = registry.GetPlugin(pluginId!);

            // Assert
            Assert.Null(plugin);
        }

        /// <summary>
        /// GetPlugin 应该忽略大小写
        /// </summary>
        [Theory]
        [InlineData("GENSHIN-DIRECTION-MARKER")]
        [InlineData("Genshin-Direction-Marker")]
        [InlineData("genshin-direction-marker")]
        public void GetPlugin_ShouldBeCaseInsensitive(string pluginId)
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugin = registry.GetPlugin(pluginId);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal("genshin-direction-marker", plugin!.Id);
        }

        #endregion


        #region GetPluginSourceDirectory Tests

        /// <summary>
        /// GetPluginSourceDirectory 应该返回正确的路径
        /// </summary>
        [Fact]
        public void GetPluginSourceDirectory_ShouldReturnCorrectPath()
        {
            // Arrange
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var path = registry.GetPluginSourceDirectory("genshin-direction-marker");

            // Assert
            var expectedPath = Path.Combine(_pluginsDir, "genshin-direction-marker");
            Assert.Equal(expectedPath, path);
        }

        #endregion

        #region PluginExists Tests

        /// <summary>
        /// PluginExists 应该返回 true 当插件存在时
        /// </summary>
        [Fact]
        public void PluginExists_ShouldReturnTrue_WhenPluginExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act & Assert
            Assert.True(registry.PluginExists("genshin-direction-marker"));
            Assert.True(registry.PluginExists("subtitle-highlight"));
        }

        /// <summary>
        /// PluginExists 应该返回 false 当插件不存在时
        /// </summary>
        [Fact]
        public void PluginExists_ShouldReturnFalse_WhenPluginNotExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);

            // Act & Assert
            Assert.False(registry.PluginExists("non-existent"));
        }

        #endregion


        #region Reload Tests

        /// <summary>
        /// Reload 应该重新加载索引
        /// </summary>
        [Fact]
        public void Reload_ShouldReloadRegistry()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new PluginRegistry(_pluginsDir);
            
            // 首次加载
            var plugins1 = registry.GetAllPlugins();
            Assert.Equal(2, plugins1.Count);

            // 修改 registry.json
            CreateRegistryFile(new
            {
                version = 1,
                plugins = new[]
                {
                    new
                    {
                        id = "new-plugin",
                        name = "新插件",
                        version = "1.0.0",
                        author = "Test",
                        description = "新添加的插件",
                        tags = Array.Empty<string>(),
                        permissions = Array.Empty<string>(),
                        profiles = Array.Empty<string>()
                    }
                }
            });

            // Act
            registry.Reload();
            var plugins2 = registry.GetAllPlugins();

            // Assert
            Assert.Single(plugins2);
            Assert.Equal("new-plugin", plugins2[0].Id);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// 处理无效的 JSON 文件
        /// </summary>
        [Fact]
        public void GetAllPlugins_ShouldReturnEmptyList_WhenJsonIsInvalid()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_pluginsDir, "registry.json"), "invalid json");
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugins = registry.GetAllPlugins();

            // Assert
            Assert.Empty(plugins);
        }

        /// <summary>
        /// 处理空的 plugins 数组
        /// </summary>
        [Fact]
        public void GetAllPlugins_ShouldReturnEmptyList_WhenPluginsArrayIsEmpty()
        {
            // Arrange
            CreateRegistryFile(new { version = 1, plugins = Array.Empty<object>() });
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugins = registry.GetAllPlugins();

            // Assert
            Assert.Empty(plugins);
        }

        /// <summary>
        /// 插件的 permissions 为空时应该正常处理
        /// </summary>
        [Fact]
        public void GetPlugin_ShouldHandleEmptyPermissions()
        {
            // Arrange
            CreateRegistryFile(new
            {
                version = 1,
                plugins = new[]
                {
                    new
                    {
                        id = "no-permissions",
                        name = "无权限插件",
                        version = "1.0.0",
                        author = "Test",
                        description = "不需要任何权限的插件",
                        tags = Array.Empty<string>(),
                        permissions = Array.Empty<string>(),
                        profiles = Array.Empty<string>()
                    }
                }
            });
            var registry = new PluginRegistry(_pluginsDir);

            // Act
            var plugin = registry.GetPlugin("no-permissions");

            // Assert
            Assert.NotNull(plugin);
            Assert.Empty(plugin!.Permissions);
        }

        #endregion
    }
}
