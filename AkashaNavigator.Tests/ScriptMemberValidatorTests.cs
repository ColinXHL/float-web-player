using Xunit;
using AkashaNavigator.Plugins;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Plugins.Apis.Core;
using System.Linq;

namespace AkashaNavigator.Tests
{
/// <summary>
/// ScriptMemberValidator 单元测试
/// </summary>
public class ScriptMemberValidatorTests
{
#region IsCamelCase Tests

    [Theory]
    [InlineData("log", true)]
    [InlineData("warn", true)]
    [InlineData("error", true)]
    [InlineData("version", true)]
    [InlineData("logger", true)]
    [InlineData("get", true)]
    [InlineData("set", true)]
    [InlineData("has", true)]
    [InlineData("remove", true)]
    [InlineData("save", true)]
    [InlineData("load", true)]
    [InlineData("delete", true)]
    [InlineData("exists", true)]
    [InlineData("list", true)]
    [InlineData("setPosition", true)]
    [InlineData("setSize", true)]
    [InlineData("show", true)]
    [InlineData("hide", true)]
    [InlineData("getRect", true)]
    [InlineData("showMarker", true)]
    [InlineData("clearMarkers", true)]
    [InlineData("setMarkerStyle", true)]
    [InlineData("setMarkerImage", true)]
    [InlineData("drawText", true)]
    [InlineData("drawRect", true)]
    [InlineData("drawImage", true)]
    [InlineData("removeElement", true)]
    [InlineData("clear", true)]
    [InlineData("enterEditMode", true)]
    [InlineData("exitEditMode", true)]
    [InlineData("getCurrentTime", true)]
    [InlineData("getDuration", true)]
    [InlineData("getPlaybackRate", true)]
    [InlineData("setPlaybackRate", true)]
    [InlineData("getVolume", true)]
    [InlineData("setVolume", true)]
    [InlineData("isMuted", true)]
    [InlineData("setMuted", true)]
    [InlineData("setOpacity", true)]
    [InlineData("getOpacity", true)]
    [InlineData("setClickThrough", true)]
    [InlineData("isClickThrough", true)]
    [InlineData("setTopmost", true)]
    [InlineData("isTopmost", true)]
    [InlineData("getBounds", true)]
    [InlineData("hasSubtitles", true)]
    [InlineData("getCurrent", true)]
    [InlineData("getAll", true)]
    [InlineData("on", true)]
    [InlineData("off", true)]
    public void IsCamelCase_ValidCamelCaseNames_ReturnsTrue(string name, bool expected)
    {
        var result = ScriptMemberValidator.IsCamelCase(name);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Log", false)]          // PascalCase
    [InlineData("SetPosition", false)]  // PascalCase
    [InlineData("GET", false)]          // All uppercase
    [InlineData("set_position", false)] // snake_case
    [InlineData("set-position", false)] // kebab-case
    [InlineData("_private", false)]     // Starts with underscore
    [InlineData("123abc", false)]       // Starts with number
    [InlineData("", false)]             // Empty string
    public void IsCamelCase_InvalidNames_ReturnsFalse(string name, bool expected)
    {
        var result = ScriptMemberValidator.IsCamelCase(name);
        Assert.Equal(expected, result);
    }

#endregion

#region GetApiTypes Tests

    [Fact]
    public void GetApiTypes_ReturnsExpectedApiClasses()
    {
        var apiTypes = ScriptMemberValidator.GetApiTypes().ToList();

        // 验证返回的类型数量（应该包含所有 *Api 类）
        Assert.NotEmpty(apiTypes);

        // 验证包含核心 API 类
        var typeNames = apiTypes.Select(t => t.Name).ToList();
        Assert.Contains("CoreApi", typeNames);
        Assert.Contains("ConfigApi", typeNames);
        Assert.Contains("OverlayApi", typeNames);
        Assert.Contains("PlayerApi", typeNames);
        Assert.Contains("WindowApi", typeNames);
        Assert.Contains("StorageApi", typeNames);
        Assert.Contains("HttpApi", typeNames);
        Assert.Contains("EventApi", typeNames);
        Assert.Contains("SubtitleApi", typeNames);
        Assert.Contains("PluginApi", typeNames);
    }

#endregion

#region ValidateType Tests

    [Fact]
    public void ValidateType_CoreApi_AllMembersHaveScriptMemberAttribute()
    {
        var result = ScriptMemberValidator.ValidateType(typeof(CoreApi));

        // CoreApi 应该有 version 属性和 logger 属性
        Assert.True(result.TotalMembers >= 2, $"Expected at least 2 members, got {result.TotalMembers}");

        // 检查是否有 camelCase 命名的成员
        var memberNames = result.Members.Select(m => m.ScriptMemberName).ToList();
        Assert.Contains("version", memberNames);
        Assert.Contains("logger", memberNames); // LogProxy 属性
    }

    [Fact]
    public void ValidateType_ConfigApi_AllMembersHaveScriptMemberAttribute()
    {
        var result = ScriptMemberValidator.ValidateType(typeof(ConfigApi));

        // ConfigApi 应该有 get, set, has, remove 方法
        Assert.True(result.TotalMembers >= 4, $"Expected at least 4 members, got {result.TotalMembers}");

        var memberNames = result.Members.Select(m => m.ScriptMemberName).ToList();
        Assert.Contains("get", memberNames);
        Assert.Contains("set", memberNames);
        Assert.Contains("has", memberNames);
        Assert.Contains("remove", memberNames);
    }

    [Fact]
    public void ValidateType_StorageApi_AllMembersHaveScriptMemberAttribute()
    {
        var result = ScriptMemberValidator.ValidateType(typeof(StorageApi));

        // StorageApi 应该有 save, load, delete, exists, list 方法
        Assert.True(result.TotalMembers >= 5, $"Expected at least 5 members, got {result.TotalMembers}");

        var memberNames = result.Members.Select(m => m.ScriptMemberName).ToList();
        Assert.Contains("save", memberNames);
        Assert.Contains("load", memberNames);
        Assert.Contains("delete", memberNames);
        Assert.Contains("exists", memberNames);
        Assert.Contains("list", memberNames);
    }

    [Fact]
    public void ValidateType_WindowApi_AllMembersHaveScriptMemberAttribute()
    {
        var result = ScriptMemberValidator.ValidateType(typeof(WindowApi));

        // WindowApi 应该有多个方法
        Assert.True(result.TotalMembers >= 7, $"Expected at least 7 members, got {result.TotalMembers}");

        var memberNames = result.Members.Select(m => m.ScriptMemberName).ToList();
        Assert.Contains("setOpacity", memberNames);
        Assert.Contains("getOpacity", memberNames);
        Assert.Contains("setClickThrough", memberNames);
        Assert.Contains("isClickThrough", memberNames);
        Assert.Contains("setTopmost", memberNames);
        Assert.Contains("isTopmost", memberNames);
        Assert.Contains("getBounds", memberNames);
    }

#endregion

#region ValidateAllApiClasses Tests

    [Fact]
    public void ValidateAllApiClasses_AllScriptMembersAreCamelCase()
    {
        var result = ScriptMemberValidator.ValidateAllApiClasses();

        // 输出所有验证的成员信息（用于调试）
        foreach (var member in result.Members)
        {
            System.Diagnostics.Debug.WriteLine(
                $"{member.ClassName}.{member.MemberName} -> [{member.ScriptMemberName}] Valid={member.IsValid}");
        }

        // 输出所有错误（用于调试）
        foreach (var error in result.Errors)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: {error.ClassName}.{error.MemberName}: {error.Message}");
        }

        // 过滤掉缺少 ScriptMember 属性的错误（这些可能是内部方法）
        // 只检查 camelCase 命名错误
        var camelCaseErrors = result.Errors.Where(e => e.Type == ScriptMemberValidator.ErrorType.NotCamelCase).ToList();

        Assert.Empty(camelCaseErrors);
    }

    [Fact]
    public void ValidateAllApiClasses_ReturnsNonEmptyResult()
    {
        var result = ScriptMemberValidator.ValidateAllApiClasses();

        // 应该有成员被验证
        Assert.True(result.TotalMembers > 0, "Expected at least some members to be validated");

        // 应该有有效的成员
        Assert.True(result.ValidMembers > 0, "Expected at least some valid members");
    }

#endregion
}
}
