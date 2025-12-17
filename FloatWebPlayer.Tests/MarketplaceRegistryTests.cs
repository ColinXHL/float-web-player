using System;
using System.IO;
using System.Linq;
using FloatWebPlayer.Models;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// 市场注册表格式验证测试
    /// </summary>
    public class MarketplaceRegistryTests
    {
        /// <summary>
        /// 验证 profiles/registry.json 格式正确
        /// **Feature: custom-profile-creation, Property 8: Marketplace profile contains required fields**
        /// **Validates: Requirements 4.2**
        /// </summary>
        [Fact]
        public void ProfilesRegistry_ShouldBeValidFormat()
        {
            // Arrange: 读取 profiles/registry.json
            var registryPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "profiles", "registry.json");
            
            // 如果文件不存在，尝试其他路径
            if (!File.Exists(registryPath))
            {
                registryPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "profiles", "registry.json");
            }
            
            Assert.True(File.Exists(registryPath), $"Registry file not found at {registryPath}");
            
            var json = File.ReadAllText(registryPath);
            
            // Act: 解析注册表
            var registry = ProfileMarketplaceRegistry.FromJson(json);
            
            // Assert: 验证注册表格式
            Assert.NotNull(registry);
            Assert.Equal(1, registry!.Version);
            Assert.False(string.IsNullOrWhiteSpace(registry.Name));
            Assert.False(string.IsNullOrWhiteSpace(registry.Description));
            Assert.NotEmpty(registry.Profiles);
        }

        /// <summary>
        /// 验证原神 Profile 包含所有必需字段
        /// **Feature: custom-profile-creation, Property 8: Marketplace profile contains required fields**
        /// **Validates: Requirements 4.2**
        /// </summary>
        [Fact]
        public void GenshinProfile_ShouldContainRequiredFields()
        {
            // Arrange: 读取 profiles/registry.json
            var registryPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "profiles", "registry.json");
            
            if (!File.Exists(registryPath))
            {
                registryPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "profiles", "registry.json");
            }
            
            Assert.True(File.Exists(registryPath), $"Registry file not found at {registryPath}");
            
            var json = File.ReadAllText(registryPath);
            var registry = ProfileMarketplaceRegistry.FromJson(json);
            Assert.NotNull(registry);
            
            // Act: 查找原神 Profile
            var genshinProfile = registry!.Profiles.FirstOrDefault(p => p.Id == "genshin");
            
            // Assert: 验证原神 Profile 包含所有必需字段
            Assert.NotNull(genshinProfile);
            Assert.Equal("genshin", genshinProfile!.Id);
            Assert.Equal("原神", genshinProfile.Name);
            Assert.False(string.IsNullOrWhiteSpace(genshinProfile.Description));
            Assert.Equal("FloatWebPlayer", genshinProfile.Author);
            Assert.Equal("原神", genshinProfile.TargetGame);
            Assert.Equal("1.0.0", genshinProfile.Version);
            Assert.NotEmpty(genshinProfile.PluginIds);
            Assert.Contains("genshin-direction-marker", genshinProfile.PluginIds);
        }

        /// <summary>
        /// 验证市场 Profile 可以转换为 MarketplaceProfile
        /// **Feature: custom-profile-creation**
        /// **Validates: Requirements 4.3**
        /// </summary>
        [Fact]
        public void MarketplaceProfileEntry_ShouldConvertToMarketplaceProfile()
        {
            // Arrange: 读取 profiles/registry.json
            var registryPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "profiles", "registry.json");
            
            if (!File.Exists(registryPath))
            {
                registryPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "..", "..", "profiles", "registry.json");
            }
            
            Assert.True(File.Exists(registryPath), $"Registry file not found at {registryPath}");
            
            var json = File.ReadAllText(registryPath);
            var registry = ProfileMarketplaceRegistry.FromJson(json);
            Assert.NotNull(registry);
            
            var genshinEntry = registry!.Profiles.FirstOrDefault(p => p.Id == "genshin");
            Assert.NotNull(genshinEntry);
            
            // Act: 转换为 MarketplaceProfile
            var sourceUrl = "https://example.com/registry.json";
            var marketplaceProfile = genshinEntry!.ToMarketplaceProfile(sourceUrl);
            
            // Assert: 验证转换结果
            Assert.NotNull(marketplaceProfile);
            Assert.Equal("genshin", marketplaceProfile.Id);
            Assert.Equal("原神", marketplaceProfile.Name);
            Assert.Equal("FloatWebPlayer", marketplaceProfile.Author);
            Assert.Equal("原神", marketplaceProfile.TargetGame);
            Assert.Equal("1.0.0", marketplaceProfile.Version);
            Assert.Equal(sourceUrl, marketplaceProfile.SourceUrl);
            Assert.Contains("genshin-direction-marker", marketplaceProfile.PluginIds);
        }
    }
}
