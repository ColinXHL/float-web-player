using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using Xunit;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins;

namespace AkashaNavigator.Tests
{
/// <summary>
/// SettingsProxy 单元测试
/// </summary>
public class SettingsProxyTests
{
#region Test Helpers

    private static PluginConfig CreateTestConfig(string pluginId = "test-plugin")
    {
        return new PluginConfig(pluginId);
    }

    private static Dictionary<string, JsonElement> CreateDefaults(Dictionary<string, object> values)
    {
        var defaults = new Dictionary<string, JsonElement>();
        foreach (var kvp in values)
        {
            var json = JsonSerializer.Serialize(kvp.Value);
            defaults[kvp.Key] = JsonDocument.Parse(json).RootElement.Clone();
        }
        return defaults;
    }

#endregion

#region TryGetMember Tests

    [Fact]
    public void TryGetMember_ReturnsDefaultValue_WhenKeyExistsInDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "theme", "dark" }, { "fontSize", 14 } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        string theme = settings.theme;
        int fontSize = settings.fontSize;

        // Assert
        Assert.Equal("dark", theme);
        Assert.Equal(14, fontSize);
    }

    [Fact]
    public void TryGetMember_ReturnsUserValue_WhenKeyExistsInConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("theme", "light");
        config.Set("fontSize", 16);
        var defaults = CreateDefaults(new Dictionary<string, object> { { "theme", "dark" }, { "fontSize", 14 } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        string theme = settings.theme;
        int fontSize = settings.fontSize;

        // Assert
        Assert.Equal("light", theme);
        Assert.Equal(16, fontSize);
    }

    [Fact]
    public void TryGetMember_ReturnsNull_WhenKeyNotFound()
    {
        // Arrange
        var config = CreateTestConfig();
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        object? result = settings.nonExistentKey;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetMember_UserConfigOverridesDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("theme", "custom");
        var defaults = CreateDefaults(new Dictionary<string, object> { { "theme", "dark" } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        string theme = settings.theme;

        // Assert
        Assert.Equal("custom", theme);
    }

#endregion

#region TrySetMember Tests

    [Fact]
    public void TrySetMember_SetsValueInConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        settings.newKey = "newValue";

        // Assert
        Assert.Equal("newValue", config.Get<string>("newKey"));
    }

    [Fact]
    public void TrySetMember_OverridesExistingValue()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("existingKey", "oldValue");
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        settings.existingKey = "newValue";

        // Assert
        Assert.Equal("newValue", config.Get<string>("existingKey"));
    }

    [Fact]
    public void TrySetMember_HandlesNumericValues()
    {
        // Arrange
        var config = CreateTestConfig();
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        settings.intValue = 42;
        settings.doubleValue = 3.14;

        // Assert
        Assert.Equal(42, config.Get<int>("intValue"));
        Assert.Equal(3.14, config.Get<double>("doubleValue"));
    }

    [Fact]
    public void TrySetMember_HandlesBooleanValues()
    {
        // Arrange
        var config = CreateTestConfig();
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        settings.enabled = true;
        settings.disabled = false;

        // Assert
        Assert.True(config.Get<bool>("enabled"));
        Assert.False(config.Get<bool>("disabled"));
    }

#endregion

#region Nested Property Tests

    [Fact]
    public void TryGetMember_SupportsNestedDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults =
            CreateDefaults(new Dictionary<string, object> { { "display.mode", "auto" }, { "display.brightness", 80 } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        dynamic display = settings.display;
        string mode = display.mode;
        int brightness = display.brightness;

        // Assert
        Assert.Equal("auto", mode);
        Assert.Equal(80, brightness);
    }

    [Fact]
    public void TrySetMember_SupportsNestedPaths()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "display.mode", "auto" } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        dynamic display = settings.display;
        display.mode = "manual";

        // Assert
        Assert.Equal("manual", config.Get<string>("display.mode"));
    }

    [Fact]
    public void TryGetMember_NestedUserConfigOverridesDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("display.mode", "custom");
        var defaults = CreateDefaults(new Dictionary<string, object> { { "display.mode", "auto" } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        dynamic display = settings.display;
        string mode = display.mode;

        // Assert
        Assert.Equal("custom", mode);
    }

#endregion

#region Type Conversion Tests

    [Fact]
    public void TryGetMember_ConvertsStringCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "name", "Test Plugin" } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        string name = settings.name;

        // Assert
        Assert.Equal("Test Plugin", name);
    }

    [Fact]
    public void TryGetMember_ConvertsIntegerCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "count", 100 } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        int count = settings.count;

        // Assert
        Assert.Equal(100, count);
    }

    [Fact]
    public void TryGetMember_ConvertsDoubleCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "ratio", 1.5 } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        double ratio = settings.ratio;

        // Assert
        Assert.Equal(1.5, ratio);
    }

    [Fact]
    public void TryGetMember_ConvertsBooleanCorrectly()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "enabled", true }, { "disabled", false } });
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        bool enabled = settings.enabled;
        bool disabled = settings.disabled;

        // Assert
        Assert.True(enabled);
        Assert.False(disabled);
    }

#endregion

#region Edge Cases

    [Fact]
    public void Constructor_ThrowsOnNullConfig()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SettingsProxy(null!, null, "test-plugin"));
    }

    [Fact]
    public void TryGetMember_HandlesNullDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("key", "value");
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        string value = settings.key;

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public void TryGetMember_HandlesEmptyDefaults()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("key", "value");
        var defaults = new Dictionary<string, JsonElement>();
        dynamic settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        string value = settings.key;

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public void TrySetMember_HandlesNullValue()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Set("key", "value");
        dynamic settings = new SettingsProxy(config, null, "test-plugin");

        // Act
        settings.key = null;

        // Assert
        Assert.False(config.ContainsKey("key"));
    }

#endregion

#region GetDynamicMemberNames Tests

    [Fact]
    public void GetDynamicMemberNames_ReturnsDefaultKeys()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults = CreateDefaults(new Dictionary<string, object> { { "theme", "dark" }, { "fontSize", 14 } });
        var settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        var names = settings.GetDynamicMemberNames();

        // Assert
        Assert.Contains("theme", names);
        Assert.Contains("fontSize", names);
    }

    [Fact]
    public void GetDynamicMemberNames_ReturnsNestedRootKeys()
    {
        // Arrange
        var config = CreateTestConfig();
        var defaults =
            CreateDefaults(new Dictionary<string, object> { { "display.mode", "auto" }, { "display.brightness", 80 } });
        var settings = new SettingsProxy(config, defaults, "test-plugin");

        // Act
        var names = settings.GetDynamicMemberNames();

        // Assert
        Assert.Contains("display", names);
    }

#endregion
}
}
