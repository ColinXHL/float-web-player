using System.IO;
using System.Text.Json;
using SandronePlayer.Models;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// PluginConfig 属性测试
    /// </summary>
    public class PluginConfigTests
    {
        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返**
        /// *对于任意*配置键值对，调用 config.set(key, value) 后再调用 config.get(key) 应返回相同的值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property SetThenGet_ShouldReturnSameValue_ForStrings(
            NonEmptyString key, NonEmptyString value)
        {
            // 排除包含空白的键（无效键）
            var validKey = !string.IsNullOrWhiteSpace(key.Get) && !key.Get.Contains(' ');

            var config = new PluginConfig("test-plugin");
            config.Set(key.Get, value.Get);
            var retrieved = config.Get<string>(key.Get);

            return (retrieved == value.Get)
                .When(validKey)
                .Label($"Set '{key.Get}' = '{value.Get}', Get = '{retrieved}'");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返（整数）**
        /// *对于任意*整数配置值，set 后 get 应返回相同值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property SetThenGet_ShouldReturnSameValue_ForIntegers(
            NonEmptyString key, int value)
        {
            var validKey = !string.IsNullOrWhiteSpace(key.Get) && !key.Get.Contains(' ');

            var config = new PluginConfig("test-plugin");
            config.Set(key.Get, value);
            var retrieved = config.Get<int>(key.Get);

            return (retrieved == value)
                .When(validKey)
                .Label($"Set '{key.Get}' = {value}, Get = {retrieved}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返（布尔）**
        /// *对于任意*布尔配置值，set 后 get 应返回相同值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property SetThenGet_ShouldReturnSameValue_ForBooleans(
            NonEmptyString key, bool value)
        {
            var validKey = !string.IsNullOrWhiteSpace(key.Get) && !key.Get.Contains(' ');

            var config = new PluginConfig("test-plugin");
            config.Set(key.Get, value);
            var retrieved = config.Get<bool>(key.Get);

            return (retrieved == value)
                .When(validKey)
                .Label($"Set '{key.Get}' = {value}, Get = {retrieved}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返（浮点数）**
        /// *对于任意*浮点数配置值，set 后 get 应返回相同值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property SetThenGet_ShouldReturnSameValue_ForDoubles(
            NonEmptyString key, NormalFloat value)
        {
            var validKey = !string.IsNullOrWhiteSpace(key.Get) && !key.Get.Contains(' ');

            var config = new PluginConfig("test-plugin");
            config.Set(key.Get, value.Get);
            var retrieved = config.Get<double>(key.Get);

            // 浮点数比较使用容差
            var isEqual = Math.Abs(retrieved - value.Get) < 0.0001;

            return isEqual
                .When(validKey)
                .Label($"Set '{key.Get}' = {value.Get}, Get = {retrieved}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返（点号路径）**
        /// *对于任意*使用点号路径的配置，set 后 get 应返回相同值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property SetThenGet_ShouldWork_WithDotNotationPath(
            NonEmptyString parent, NonEmptyString child, int value)
        {
            // 排除包含空白或点号的键部分
            var validParent = !string.IsNullOrWhiteSpace(parent.Get) && 
                              !parent.Get.Contains(' ') && 
                              !parent.Get.Contains('.');
            var validChild = !string.IsNullOrWhiteSpace(child.Get) && 
                             !child.Get.Contains(' ') && 
                             !child.Get.Contains('.');

            var key = $"{parent.Get}.{child.Get}";
            var config = new PluginConfig("test-plugin");
            config.Set(key, value);
            var retrieved = config.Get<int>(key);

            return (retrieved == value)
                .When(validParent && validChild)
                .Label($"Set '{key}' = {value}, Get = {retrieved}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 4: 配置持久化往返（文件往返）**
        /// *对于任意*配置，保存到文件后重新加载应保持相同值
        /// **Validates: Requirements 2.2, 2.3**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property SaveAndLoad_ShouldPreserveValues(
            NonEmptyString pluginId, NonEmptyString key, int value, bool enabled)
        {
            var validPluginId = !string.IsNullOrWhiteSpace(pluginId.Get);
            var validKey = !string.IsNullOrWhiteSpace(key.Get) && !key.Get.Contains(' ');

            // 使用临时文件
            var tempFile = Path.Combine(Path.GetTempPath(), $"plugin_config_test_{Guid.NewGuid()}.json");

            try
            {
                var config = new PluginConfig(pluginId.Get)
                {
                    Enabled = enabled
                };
                config.Set(key.Get, value);
                config.SaveToFile(tempFile);

                var loadedConfig = PluginConfig.LoadFromFile(tempFile, pluginId.Get);

                var pluginIdMatch = loadedConfig.PluginId == pluginId.Get;
                var enabledMatch = loadedConfig.Enabled == enabled;
                var valueMatch = loadedConfig.Get<int>(key.Get) == value;

                return (pluginIdMatch && enabledMatch && valueMatch)
                    .When(validPluginId && validKey)
                    .Label($"PluginId: {pluginIdMatch}, Enabled: {enabledMatch}, Value: {valueMatch}");
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        /// <summary>
        /// 获取不存在的键应返回默认值
        /// </summary>
        [Fact]
        public void Get_NonExistentKey_ShouldReturnDefault()
        {
            var config = new PluginConfig("test-plugin");
            
            Assert.Equal(0, config.Get<int>("nonexistent"));
            Assert.Equal("default", config.Get("nonexistent", "default"));
            Assert.False(config.Get<bool>("nonexistent"));
        }

        /// <summary>
        /// 嵌套路径应正确工作
        /// </summary>
        [Fact]
        public void NestedPath_ShouldWorkCorrectly()
        {
            var config = new PluginConfig("test-plugin");
            
            config.Set("overlay.position.x", 100);
            config.Set("overlay.position.y", 200);
            config.Set("overlay.size", 50);

            Assert.Equal(100, config.Get<int>("overlay.position.x"));
            Assert.Equal(200, config.Get<int>("overlay.position.y"));
            Assert.Equal(50, config.Get<int>("overlay.size"));
        }

        /// <summary>
        /// ContainsKey 应正确检测键存在
        /// </summary>
        [Fact]
        public void ContainsKey_ShouldDetectExistence()
        {
            var config = new PluginConfig("test-plugin");
            
            config.Set("existing", "value");
            
            Assert.True(config.ContainsKey("existing"));
            Assert.False(config.ContainsKey("nonexistent"));
        }

        /// <summary>
        /// Remove 应正确移除键
        /// </summary>
        [Fact]
        public void Remove_ShouldDeleteKey()
        {
            var config = new PluginConfig("test-plugin");
            
            config.Set("toRemove", "value");
            Assert.True(config.ContainsKey("toRemove"));
            
            config.Remove("toRemove");
            Assert.False(config.ContainsKey("toRemove"));
        }
    }
}
