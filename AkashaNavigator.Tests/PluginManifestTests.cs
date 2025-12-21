using System.Text.Json;
using AkashaNavigator.Models.Plugin;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
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
    public Property MissingRequiredFields_ShouldFailValidation(bool hasId, bool hasName, bool hasVersion, bool hasMain,
                                                               NonEmptyString id, NonEmptyString name,
                                                               NonEmptyString version, NonEmptyString main)
    {
        // 至少缺少一个必需字段
        var atLeastOneMissing = !hasId || !hasName || !hasVersion || !hasMain;

        // 构建 JSON 对象
        var jsonObj = new Dictionary<string, object>();
        if (hasId)
            jsonObj["id"] = id.Get;
        if (hasName)
            jsonObj["name"] = name.Get;
        if (hasVersion)
            jsonObj["version"] = version.Get;
        if (hasMain)
            jsonObj["main"] = main.Get;

        var json = JsonSerializer.Serialize(jsonObj);
        var result = PluginManifest.LoadFromJson(json);

        // 验证：加载应失败
        var loadFailed = !result.IsSuccess;

        // 验证：错误消息应包含缺失字段信息
        var hasMissingFieldInfo = result.ValidationResult != null && result.ValidationResult.MissingFields.Any();

        // 验证：缺失的字段应与实际缺失的字段匹配
        var missingFields = new List<string>();
        if (!hasId)
            missingFields.Add("id");
        if (!hasName)
            missingFields.Add("name");
        if (!hasVersion)
            missingFields.Add("version");
        if (!hasMain)
            missingFields.Add("main");

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
    public Property ValidManifest_ShouldLoadSuccessfully(NonEmptyString id, NonEmptyString name, NonEmptyString version,
                                                         NonEmptyString main)
    {
        // 排除只包含空白字符的字符串（这些不是有效的必需字段值）
        var allFieldsNonWhitespace = !string.IsNullOrWhiteSpace(id.Get) && !string.IsNullOrWhiteSpace(name.Get) &&
                                     !string.IsNullOrWhiteSpace(version.Get) && !string.IsNullOrWhiteSpace(main.Get);

        // 构建包含所有必需字段的 JSON
        var jsonObj = new Dictionary<string, object> { ["id"] = id.Get, ["name"] = name.Get, ["version"] = version.Get,
                                                       ["main"] = main.Get };

        var json = JsonSerializer.Serialize(jsonObj);
        var result = PluginManifest.LoadFromJson(json);

        // 只有当所有字段都是非空白时才验证
        var allMatch = result.IsSuccess && result.Manifest?.Id == id.Get && result.Manifest?.Name == name.Get &&
                       result.Manifest?.Version == version.Get && result.Manifest?.Main == main.Get;

        return allMatch.When(allFieldsNonWhitespace)
            .Label($"加载成功且字段匹配 (id={id.Get}, name={name.Get}, version={version.Get}, main={main.Get})");
    }

    /// <summary>
    /// **Feature: game-plugin-system, Property 5: 清单验证（空白字符串）**
    /// *对于任意*必需字段为空白字符串的 plugin.json，插件加载应失败
    /// **Validates: Requirements 6.2, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespaceRequiredFields_ShouldFailValidation(NonEmptyString validId, NonEmptyString validName,
                                                                  NonEmptyString validVersion, NonEmptyString validMain,
                                                                  int fieldToMakeWhitespace)
    {
        // 选择一个字段设为空白
        var fieldIndex = Math.Abs(fieldToMakeWhitespace) % 4;

        var jsonObj = new Dictionary<string, object> { ["id"] = fieldIndex == 0 ? "   " : validId.Get,
                                                       ["name"] = fieldIndex == 1 ? "   " : validName.Get,
                                                       ["version"] = fieldIndex == 2 ? "   " : validVersion.Get,
                                                       ["main"] = fieldIndex == 3 ? "   " : validMain.Get };

        var json = JsonSerializer.Serialize(jsonObj);
        var result = PluginManifest.LoadFromJson(json);

        var expectedMissingField = fieldIndex switch {
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

    /// <summary>
    /// Library 字段应正确反序列化
    /// </summary>
    [Fact]
    public void Library_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""id"": ""test-plugin"",
            ""name"": ""Test Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js"",
            ""library"": [""./lib"", ""./node_modules"", ""shared/utils""]
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest?.Library);
        Assert.Equal(3, result.Manifest!.Library!.Count);
        Assert.Contains("./lib", result.Manifest.Library);
        Assert.Contains("./node_modules", result.Manifest.Library);
        Assert.Contains("shared/utils", result.Manifest.Library);
    }

    /// <summary>
    /// HttpAllowedUrls 字段应正确反序列化
    /// </summary>
    [Fact]
    public void HttpAllowedUrls_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""id"": ""test-plugin"",
            ""name"": ""Test Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js"",
            ""http_allowed_urls"": [
                ""https://api.example.com/*"",
                ""https://cdn.example.com/assets/*"",
                ""https://specific.url.com/path""
            ]
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest?.HttpAllowedUrls);
        Assert.Equal(3, result.Manifest!.HttpAllowedUrls!.Count);
        Assert.Contains("https://api.example.com/*", result.Manifest.HttpAllowedUrls);
        Assert.Contains("https://cdn.example.com/assets/*", result.Manifest.HttpAllowedUrls);
        Assert.Contains("https://specific.url.com/path", result.Manifest.HttpAllowedUrls);
    }

    /// <summary>
    /// DefaultConfig 字段应正确反序列化
    /// </summary>
    [Fact]
    public void DefaultConfig_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""id"": ""test-plugin"",
            ""name"": ""Test Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js"",
            ""defaultConfig"": {
                ""theme"": ""dark"",
                ""fontSize"": 14,
                ""enabled"": true
            }
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest?.DefaultConfig);
        Assert.Equal(3, result.Manifest!.DefaultConfig!.Count);
        Assert.True(result.Manifest.DefaultConfig.ContainsKey("theme"));
        Assert.True(result.Manifest.DefaultConfig.ContainsKey("fontSize"));
        Assert.True(result.Manifest.DefaultConfig.ContainsKey("enabled"));
        Assert.Equal("dark", result.Manifest.DefaultConfig["theme"].GetString());
        Assert.Equal(14, result.Manifest.DefaultConfig["fontSize"].GetInt32());
        Assert.True(result.Manifest.DefaultConfig["enabled"].GetBoolean());
    }

    /// <summary>
    /// 完整的 plugin.json 应正确反序列化所有新字段
    /// </summary>
    [Fact]
    public void FullManifest_WithAllNewFields_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""id"": ""my-plugin"",
            ""name"": ""My Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js"",
            ""description"": ""A test plugin"",
            ""author"": ""Test Author"",
            ""permissions"": [""overlay"", ""player"", ""http""],
            ""library"": [""./lib"", ""./node_modules""],
            ""http_allowed_urls"": [
                ""https://api.example.com/*"",
                ""https://cdn.example.com/assets/*""
            ],
            ""defaultConfig"": {
                ""theme"": ""dark"",
                ""fontSize"": 14,
                ""display"": {
                    ""mode"": ""auto""
                }
            }
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        var manifest = result.Manifest!;

        // 验证基本字段
        Assert.Equal("my-plugin", manifest.Id);
        Assert.Equal("My Plugin", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("main.js", manifest.Main);
        Assert.Equal("A test plugin", manifest.Description);
        Assert.Equal("Test Author", manifest.Author);

        // 验证权限
        Assert.NotNull(manifest.Permissions);
        Assert.Equal(3, manifest.Permissions!.Count);

        // 验证 Library
        Assert.NotNull(manifest.Library);
        Assert.Equal(2, manifest.Library!.Count);

        // 验证 HttpAllowedUrls
        Assert.NotNull(manifest.HttpAllowedUrls);
        Assert.Equal(2, manifest.HttpAllowedUrls!.Count);

        // 验证 DefaultConfig
        Assert.NotNull(manifest.DefaultConfig);
        Assert.Equal(3, manifest.DefaultConfig!.Count);
    }

    /// <summary>
    /// 缺少可选的新字段时应正常加载
    /// </summary>
    [Fact]
    public void MissingOptionalNewFields_ShouldLoadSuccessfully()
    {
        var json = @"{
            ""id"": ""test-plugin"",
            ""name"": ""Test Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js""
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Manifest?.Library);
        Assert.Null(result.Manifest?.HttpAllowedUrls);
        Assert.Null(result.Manifest?.DefaultConfig);
    }

    /// <summary>
    /// 空数组的新字段应正确反序列化
    /// </summary>
    [Fact]
    public void EmptyArrayNewFields_ShouldDeserializeCorrectly()
    {
        var json = @"{
            ""id"": ""test-plugin"",
            ""name"": ""Test Plugin"",
            ""version"": ""1.0.0"",
            ""main"": ""main.js"",
            ""library"": [],
            ""http_allowed_urls"": [],
            ""defaultConfig"": {}
        }";

        var result = PluginManifest.LoadFromJson(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest?.Library);
        Assert.Empty(result.Manifest!.Library!);
        Assert.NotNull(result.Manifest.HttpAllowedUrls);
        Assert.Empty(result.Manifest.HttpAllowedUrls!);
        Assert.NotNull(result.Manifest.DefaultConfig);
        Assert.Empty(result.Manifest.DefaultConfig!);
    }
}
}
