using System.Text.Json;
using SandronePlayer.Models;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// PluginManifest 属性测试
    /// </summary>
    public class PluginManifestTests
    {
        /// <summary>
        /// **Feature: game-plugin-system, Property 5: 清单验证**
        /// *对于任意*缺少必需字段（id、name、version、main）的 plugin.json，插件加载应失败并报告具体错误
        /// **Validates: Requirements 6.2, 6.4**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property MissingRequiredFields_ShouldFailValidation(
            bool hasId, bool hasName, bool hasVersion, bool hasMain,
            NonEmptyString id, NonEmptyString name, NonEmptyString version, NonEmptyString main)
        {
            // 至少缺少一个必需字段
            var atLeastOneMissing = !hasId || !hasName || !hasVersion || !hasMain;

            // 构建 JSON 对象
            var jsonObj = new Dictionary<string, object>();
            if (hasId) jsonObj["id"] = id.Get;
            if (hasName) jsonObj["name"] = name.Get;
            if (hasVersion) jsonObj["version"] = version.Get;
            if (hasMain) jsonObj["main"] = main.Get;

            var json = JsonSerializer.Serialize(jsonObj);
            var result = PluginManifest.LoadFromJson(json);

            // 验证：加载应失败
            var loadFailed = !result.IsSuccess;

            // 验证：错误消息应包含缺失字段信息
            var hasMissingFieldInfo = result.ValidationResult != null &&
                result.ValidationResult.MissingFields.Any();

            // 验证：缺失的字段应与实际缺失的字段匹配
            var missingFields = new List<string>();
            if (!hasId) missingFields.Add("id");
            if (!hasName) missingFields.Add("name");
            if (!hasVersion) missingFields.Add("version");
            if (!hasMain) missingFields.Add("main");

            var reportedFieldsMatch = result.ValidationResult != null &&
                missingFields.All(f => result.ValidationResult.MissingFields.Contains(f));

            return (loadFailed && hasMissingFieldInfo && reportedFieldsMatch)
                .When(atLeastOneMissing)
                .Label($"加载失败: {loadFailed}, 有缺失字段信息: {hasMissingFieldInfo}, 字段匹配: {reportedFieldsMatch}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 5: 清单验证（补充）**
        /// *对于任意*包含所有必需字段的有效 plugin.json，插件加载应成功
        /// **Validates: Requirements 6.2**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property ValidManifest_ShouldLoadSuccessfully(
            NonEmptyString id, NonEmptyString name, NonEmptyString version, NonEmptyString main)
        {
            // 排除只包含空白字符的字符串（这些不是有效的必需字段值）
            var allFieldsNonWhitespace = !string.IsNullOrWhiteSpace(id.Get) &&
                                         !string.IsNullOrWhiteSpace(name.Get) &&
                                         !string.IsNullOrWhiteSpace(version.Get) &&
                                         !string.IsNullOrWhiteSpace(main.Get);

            // 构建包含所有必需字段的 JSON
            var jsonObj = new Dictionary<string, object>
            {
                ["id"] = id.Get,
                ["name"] = name.Get,
                ["version"] = version.Get,
                ["main"] = main.Get
            };

            var json = JsonSerializer.Serialize(jsonObj);
            var result = PluginManifest.LoadFromJson(json);

            // 只有当所有字段都是非空白时才验证
            var allMatch = result.IsSuccess &&
                           result.Manifest?.Id == id.Get &&
                           result.Manifest?.Name == name.Get &&
                           result.Manifest?.Version == version.Get &&
                           result.Manifest?.Main == main.Get;

            return allMatch
                .When(allFieldsNonWhitespace)
                .Label($"加载成功且字段匹配 (id={id.Get}, name={name.Get}, version={version.Get}, main={main.Get})");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 5: 清单验证（空白字符串）**
        /// *对于任意*必需字段为空白字符串的 plugin.json，插件加载应失败
        /// **Validates: Requirements 6.2, 6.4**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property WhitespaceRequiredFields_ShouldFailValidation(
            NonEmptyString validId, NonEmptyString validName, NonEmptyString validVersion, NonEmptyString validMain,
            int fieldToMakeWhitespace)
        {
            // 选择一个字段设为空白
            var fieldIndex = Math.Abs(fieldToMakeWhitespace) % 4;

            var jsonObj = new Dictionary<string, object>
            {
                ["id"] = fieldIndex == 0 ? "   " : validId.Get,
                ["name"] = fieldIndex == 1 ? "   " : validName.Get,
                ["version"] = fieldIndex == 2 ? "   " : validVersion.Get,
                ["main"] = fieldIndex == 3 ? "   " : validMain.Get
            };

            var json = JsonSerializer.Serialize(jsonObj);
            var result = PluginManifest.LoadFromJson(json);

            var expectedMissingField = fieldIndex switch
            {
                0 => "id",
                1 => "name",
                2 => "version",
                3 => "main",
                _ => ""
            };

            return (!result.IsSuccess)
                .Label("加载应失败")
                .And((result.ValidationResult?.MissingFields.Contains(expectedMissingField) ?? false)
                    .Label($"应报告 {expectedMissingField} 缺失"));
        }

        /// <summary>
        /// 无效 JSON 格式应返回错误
        /// </summary>
        [Fact]
        public void InvalidJson_ShouldReturnError()
        {
            var result = PluginManifest.LoadFromJson("{ invalid json }");
            Assert.False(result.IsSuccess);
            Assert.Contains("JSON", result.ErrorMessage);
        }

        /// <summary>
        /// 空 JSON 应返回错误
        /// </summary>
        [Fact]
        public void EmptyJson_ShouldReturnError()
        {
            var result = PluginManifest.LoadFromJson("");
            Assert.False(result.IsSuccess);
        }
    }
}
