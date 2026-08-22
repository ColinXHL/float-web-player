using System.Text.Json;
using AkashaNavigator.Models.Config;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// SettingsUiDefinition 单元测试
/// </summary>
public class SettingsUiDefinitionTests
{
#region LoadFromJson Tests

    [Fact]
    public void LoadFromJson_ValidJson_ReturnsDefinition()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""基本设置"",
                        ""items"": [
                            {
                                ""type"": ""checkbox"",
                                ""key"": ""enabled"",
                                ""label"": ""启用插件"",
                                ""default"": true
                            }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition);
        Assert.NotNull(definition.Sections);
        var section = Assert.Single(definition.Sections);
        Assert.Equal("基本设置", section.Title);
        Assert.NotNull(section.Items);
        var item = Assert.Single(section.Items);
        Assert.Equal("checkbox", item.Type);
        Assert.Equal("enabled", item.Key);
    }

    [Fact]
    public void LoadFromJson_EmptyJson_ReturnsNull()
    {
        // Act
        var definition = SettingsUiDefinition.LoadFromJson("");

        // Assert
        Assert.Null(definition);
    }

    [Fact]
    public void LoadFromJson_InvalidJson_ReturnsNull()
    {
        // Act
        var definition = SettingsUiDefinition.LoadFromJson("{ invalid json }");

        // Assert
        Assert.Null(definition);
    }

    [Fact]
    public void LoadFromJson_NullJson_ReturnsNull()
    {
        // Act
        var definition = SettingsUiDefinition.LoadFromJson(null!);

        // Assert
        Assert.Null(definition);
    }

#endregion

#region SettingsItem Tests

    [Fact]
    public void SettingsItem_GetDefaultValue_ReturnsCorrectType()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            { ""type"": ""checkbox"", ""key"": ""bool"", ""default"": true },
                            { ""type"": ""number"", ""key"": ""num"", ""default"": 42 },
                            { ""type"": ""text"", ""key"": ""str"", ""default"": ""hello"" },
                            { ""type"": ""slider"", ""key"": ""dbl"", ""default"": 3.14 }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var items = definition!.Sections![0].Items!;

        Assert.True(items[0].GetDefaultValue<bool>());
        Assert.Equal(42, items[1].GetDefaultValue<int>());
        Assert.Equal("hello", items[2].GetDefaultValue<string>());
        Assert.Equal(3.14, items[3].GetDefaultValue<double>(), 2);
    }

    [Fact]
    public void SettingsItem_GetDefaultValue_NoDefault_ReturnsDefault()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            { ""type"": ""checkbox"", ""key"": ""bool"" }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var item = definition!.Sections![0].Items![0];
        Assert.False(item.GetDefaultValue<bool>());
    }

#endregion

#region SelectOption Tests

    [Fact]
    public void LoadFromJson_WithSelectOptions_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            {
                                ""type"": ""select"",
                                ""key"": ""style"",
                                ""label"": ""显示样式"",
                                ""options"": [
                                    { ""value"": ""arrow"", ""label"": ""箭头"" },
                                    { ""value"": ""text"", ""label"": ""文字"" }
                                ],
                                ""default"": ""arrow""
                            }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var item = definition!.Sections![0].Items![0];
        Assert.Equal("select", item.Type);
        Assert.NotNull(item.Options);
        Assert.Equal(2, item.Options.Count);
        Assert.Equal("arrow", item.Options[0].Value);
        Assert.Equal("箭头", item.Options[0].Label);
    }

#endregion

#region NumberBox Constraints Tests

    [Fact]
    public void LoadFromJson_WithNumberConstraints_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            {
                                ""type"": ""number"",
                                ""key"": ""duration"",
                                ""label"": ""显示时长"",
                                ""default"": 3000,
                                ""min"": 0,
                                ""max"": 10000,
                                ""step"": 100
                            }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var item = definition!.Sections![0].Items![0];
        Assert.Equal(0, item.Min);
        Assert.Equal(10000, item.Max);
        Assert.Equal(100, item.Step);
    }

#endregion

#region Button Action Tests

    [Fact]
    public void LoadFromJson_WithButtonAction_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            {
                                ""type"": ""button"",
                                ""label"": ""调整位置"",
                                ""action"": ""enterEditMode""
                            }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var item = definition!.Sections![0].Items![0];
        Assert.Equal("button", item.Type);
        Assert.Equal("enterEditMode", item.Action);
    }

    [Fact]
    public void SettingsButtonActions_IsBuiltInAction_ReturnsCorrectly()
    {
        // Assert
        Assert.True(SettingsButtonActions.IsBuiltInAction("enterEditMode"));
        Assert.True(SettingsButtonActions.IsBuiltInAction("resetConfig"));
        Assert.True(SettingsButtonActions.IsBuiltInAction("openPluginFolder"));
        Assert.True(SettingsButtonActions.IsBuiltInAction("updatePluginResources"));
        Assert.False(SettingsButtonActions.IsBuiltInAction("customAction"));
        Assert.False(SettingsButtonActions.IsBuiltInAction(null));
        Assert.False(SettingsButtonActions.IsBuiltInAction(""));
    }

#endregion

#region Group Tests

    [Fact]
    public void LoadFromJson_WithNestedGroup_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""Test"",
                        ""items"": [
                            {
                                ""type"": ""group"",
                                ""label"": ""覆盖层设置"",
                                ""items"": [
                                    { ""type"": ""number"", ""key"": ""x"", ""label"": ""X 坐标"" },
                                    { ""type"": ""number"", ""key"": ""y"", ""label"": ""Y 坐标"" }
                                ]
                            }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections?[0].Items);
        var groupItem = definition!.Sections![0].Items![0];
        Assert.Equal("group", groupItem.Type);
        Assert.Equal("覆盖层设置", groupItem.Label);
        Assert.NotNull(groupItem.Items);
        Assert.Equal(2, groupItem.Items.Count);
        Assert.Equal("x", groupItem.Items[0].Key);
        Assert.Equal("y", groupItem.Items[1].Key);
    }

#endregion

#region Presentation Metadata Tests

    [Fact]
    public void LoadFromJson_PresentationMetadata_ParsesCorrectly()
    {
        var json = """
                   {
                     "sections": [
                       {
                         "title": "名单",
                         "items": [
                           {
                             "type": "textarea",
                             "key": "whitelist",
                             "label": "白名单",
                             "description": "每行一个完整名称",
                             "default": ""
                           },
                           {
                             "type": "button",
                             "label": "打开目录",
                             "action": "openPluginFolder",
                             "relativePath": "worker/win-x64/Assets/Config/Pick"
                           }
                         ]
                       }
                     ]
                   }
                   """;

        var definition = SettingsUiDefinition.LoadFromJson(json);

        var items = definition!.Sections![0].Items!;
        Assert.Equal("每行一个完整名称", items[0].Description);
        Assert.Equal("worker/win-x64/Assets/Config/Pick", items[1].RelativePath);
    }

#endregion

#region Multiple Sections Tests

    [Fact]
    public void LoadFromJson_MultipleSections_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
                ""sections"": [
                    {
                        ""title"": ""基本设置"",
                        ""items"": [
                            { ""type"": ""checkbox"", ""key"": ""enabled"", ""label"": ""启用"" }
                        ]
                    },
                    {
                        ""title"": ""高级设置"",
                        ""items"": [
                            { ""type"": ""number"", ""key"": ""timeout"", ""label"": ""超时"" }
                        ]
                    }
                ]
            }";

        // Act
        var definition = SettingsUiDefinition.LoadFromJson(json);

        // Assert
        Assert.NotNull(definition?.Sections);
        Assert.Equal(2, definition!.Sections!.Count);
        Assert.Equal("基本设置", definition.Sections[0].Title);
        Assert.Equal("高级设置", definition.Sections[1].Title);
    }

#endregion
}
}
