using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Services;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// ProfileRegistry 单元测试
    /// </summary>
    public class ProfileRegistryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _profilesDir;

        public ProfileRegistryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"profile_registry_test_{Guid.NewGuid()}");
            _profilesDir = Path.Combine(_tempDir, "Profiles");
            Directory.CreateDirectory(_profilesDir);
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
            File.WriteAllText(Path.Combine(_profilesDir, "registry.json"), json);
        }

        /// <summary>
        /// 创建标准测试数据
        /// </summary>
        private object CreateStandardRegistryData()
        {
            return new
            {
                version = 1,
                profiles = new[]
                {
                    new
                    {
                        id = "default",
                        name = "默认",
                        icon = "📺",
                        description = "通用配置",
                        recommendedPlugins = Array.Empty<string>()
                    },
                    new
                    {
                        id = "genshin",
                        name = "原神",
                        icon = "🎮",
                        description = "原神游戏配置",
                        recommendedPlugins = new[] { "genshin-direction-marker" }
                    }
                }
            };
        }

        #region GetAllProfiles Tests

        /// <summary>
        /// GetAllProfiles 应该返回所有内置 Profile
        /// </summary>
        [Fact]
        public void GetAllProfiles_ShouldReturnAllProfiles_WhenRegistryExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profiles = registry.GetAllProfiles();

            // Assert
            Assert.Equal(2, profiles.Count);
            Assert.Contains(profiles, p => p.Id == "default");
            Assert.Contains(profiles, p => p.Id == "genshin");
        }

        /// <summary>
        /// GetAllProfiles 应该返回空列表当索引文件不存在时
        /// </summary>
        [Fact]
        public void GetAllProfiles_ShouldReturnEmptyList_WhenRegistryNotExists()
        {
            // Arrange - 不创建 registry.json
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profiles = registry.GetAllProfiles();

            // Assert
            Assert.Empty(profiles);
        }

        /// <summary>
        /// GetAllProfiles 应该返回副本而非原始列表
        /// </summary>
        [Fact]
        public void GetAllProfiles_ShouldReturnCopy_NotOriginalList()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profiles1 = registry.GetAllProfiles();
            var profiles2 = registry.GetAllProfiles();

            // Assert - 修改一个列表不应影响另一个
            profiles1.Clear();
            Assert.Equal(2, profiles2.Count);
        }

        #endregion

        #region GetProfile Tests

        /// <summary>
        /// GetProfile 应该返回正确的 Profile
        /// </summary>
        [Fact]
        public void GetProfile_ShouldReturnProfile_WhenExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profile = registry.GetProfile("genshin");

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("genshin", profile!.Id);
            Assert.Equal("原神", profile.Name);
            Assert.Equal("🎮", profile.Icon);
            Assert.Contains("genshin-direction-marker", profile.RecommendedPlugins);
        }

        /// <summary>
        /// GetProfile 应该返回 null 当 Profile 不存在时
        /// </summary>
        [Fact]
        public void GetProfile_ShouldReturnNull_WhenNotExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profile = registry.GetProfile("non-existent");

            // Assert
            Assert.Null(profile);
        }

        /// <summary>
        /// GetProfile 应该返回 null 当 ID 为空时
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetProfile_ShouldReturnNull_WhenIdIsNullOrEmpty(string? profileId)
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profile = registry.GetProfile(profileId!);

            // Assert
            Assert.Null(profile);
        }

        /// <summary>
        /// GetProfile 应该忽略大小写
        /// </summary>
        [Theory]
        [InlineData("GENSHIN")]
        [InlineData("Genshin")]
        [InlineData("genshin")]
        public void GetProfile_ShouldBeCaseInsensitive(string profileId)
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profile = registry.GetProfile(profileId);

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("genshin", profile!.Id);
        }

        #endregion

        #region GetProfileTemplateDirectory Tests

        /// <summary>
        /// GetProfileTemplateDirectory 应该返回正确的路径
        /// </summary>
        [Fact]
        public void GetProfileTemplateDirectory_ShouldReturnCorrectPath()
        {
            // Arrange
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var path = registry.GetProfileTemplateDirectory("genshin");

            // Assert
            var expectedPath = Path.Combine(_profilesDir, "genshin");
            Assert.Equal(expectedPath, path);
        }

        #endregion

        #region ProfileExists Tests

        /// <summary>
        /// ProfileExists 应该返回 true 当 Profile 存在时
        /// </summary>
        [Fact]
        public void ProfileExists_ShouldReturnTrue_WhenProfileExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act & Assert
            Assert.True(registry.ProfileExists("genshin"));
            Assert.True(registry.ProfileExists("default"));
        }

        /// <summary>
        /// ProfileExists 应该返回 false 当 Profile 不存在时
        /// </summary>
        [Fact]
        public void ProfileExists_ShouldReturnFalse_WhenProfileNotExists()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act & Assert
            Assert.False(registry.ProfileExists("non-existent"));
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
            var registry = new ProfileRegistry(_profilesDir);
            
            // 首次加载
            var profiles1 = registry.GetAllProfiles();
            Assert.Equal(2, profiles1.Count);

            // 修改 registry.json
            CreateRegistryFile(new
            {
                version = 1,
                profiles = new[]
                {
                    new
                    {
                        id = "new-profile",
                        name = "新 Profile",
                        icon = "🆕",
                        description = "新添加的 Profile",
                        recommendedPlugins = Array.Empty<string>()
                    }
                }
            });

            // Act
            registry.Reload();
            var profiles2 = registry.GetAllProfiles();

            // Assert
            Assert.Single(profiles2);
            Assert.Equal("new-profile", profiles2[0].Id);
        }

        #endregion

        #region Edge Cases

        /// <summary>
        /// 处理无效的 JSON 文件
        /// </summary>
        [Fact]
        public void GetAllProfiles_ShouldReturnEmptyList_WhenJsonIsInvalid()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_profilesDir, "registry.json"), "invalid json");
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profiles = registry.GetAllProfiles();

            // Assert
            Assert.Empty(profiles);
        }

        /// <summary>
        /// 处理空的 profiles 数组
        /// </summary>
        [Fact]
        public void GetAllProfiles_ShouldReturnEmptyList_WhenProfilesArrayIsEmpty()
        {
            // Arrange
            CreateRegistryFile(new { version = 1, profiles = Array.Empty<object>() });
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profiles = registry.GetAllProfiles();

            // Assert
            Assert.Empty(profiles);
        }

        /// <summary>
        /// Profile 的 recommendedPlugins 为空时应该正常处理
        /// </summary>
        [Fact]
        public void GetProfile_ShouldHandleEmptyRecommendedPlugins()
        {
            // Arrange
            CreateRegistryFile(CreateStandardRegistryData());
            var registry = new ProfileRegistry(_profilesDir);

            // Act
            var profile = registry.GetProfile("default");

            // Assert
            Assert.NotNull(profile);
            Assert.Empty(profile!.RecommendedPlugins);
        }

        #endregion
    }
}
