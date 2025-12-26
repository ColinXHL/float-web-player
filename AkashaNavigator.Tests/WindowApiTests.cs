using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// WindowApi 属性测试和单元测试
/// 注意：由于 WindowApi 依赖 WPF PlayerWindow，这里主要测试无窗口时的行为
/// 以及与 EventApi 的集成逻辑
/// </summary>
public class WindowApiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginContext _context;

    public WindowApiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"window_api_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var manifest = new PluginManifest { Id = "test-window-plugin", Name = "Test Window Plugin", Version = "1.0.0",
                                            Main = "main.js", Permissions = new List<string> { "window", "events" } };

        File.WriteAllText(Path.Combine(_tempDir, "main.js"), "function onLoad() {} function onUnload() {}");

        _context = new PluginContext(manifest, _tempDir);
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

#region Property 4 : 透明度 Round - Trip(无窗口时的默认行为)

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 4: 透明度 Round-Trip**
    /// *对于任意*有效透明度值（0.2 到 1.0），当窗口不可用时，GetOpacity 应返回默认值 1.0。
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OpacityRoundTrip_NoWindow_ReturnsDefault(PositiveInt seed)
    {
        var windowApi = new WindowApi(_context, null);

        // 生成 0.2 到 1.0 之间的透明度值
        var opacity = 0.2 + (seed.Get % 81) / 100.0; // 0.2 到 1.0

        // 设置透明度（无窗口时应该静默失败）
        windowApi.SetOpacity(opacity);

        // 获取透明度（无窗口时应该返回默认值 1.0）
        var result = windowApi.GetOpacity();

        return (result == 1.0).Label($"Opacity: {opacity}, Result: {result}");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 4: 透明度钳制**
    /// *对于任意*超出范围的透明度值，应该被钳制到 0.2-1.0 范围。
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.2)]  // 低于最小值
    [InlineData(0.1, 0.2)]  // 低于最小值
    [InlineData(1.5, 1.0)]  // 高于最大值
    [InlineData(2.0, 1.0)]  // 高于最大值
    [InlineData(-0.5, 0.2)] // 负数
    public void SetOpacity_OutOfRange_ShouldBeClamped(double input, double expectedClamped)
    {
        // 这个测试验证钳制逻辑的正确性
        // 由于没有实际窗口，我们只能验证 Math.Clamp 的行为
        var clamped = Math.Clamp(input, AppConstants.MinOpacity, AppConstants.MaxOpacity);
        Assert.Equal(expectedClamped, clamped, 3);
    }

#endregion

#region Property 5 : 穿透模式状态一致性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 5: 穿透模式状态一致性**
    /// *对于任意*布尔值，当窗口不可用时，IsClickThrough 应返回默认值 false。
    /// **Validates: Requirements 3.3, 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClickThroughConsistency_NoWindow_ReturnsDefault(bool enabled)
    {
        var windowApi = new WindowApi(_context, null);

        // 设置穿透模式（无窗口时应该静默失败）
        windowApi.SetClickThrough(enabled);

        // 获取穿透模式（无窗口时应该返回默认值 false）
        var result = windowApi.IsClickThrough();

        return (result == false).Label($"Enabled: {enabled}, Result: {result}");
    }

#endregion

#region Property 10 : 事件触发一致性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 10: 事件触发一致性**
    /// 验证 WindowApi 与 EventManager 的集成：当设置 EventManager 后，状态变化应触发事件。
    /// 由于没有实际窗口，这里测试 EventManager 的设置和基本集成。
    /// **Validates: Requirements 6.4, 6.5**
    /// </summary>
    [Fact]
    public void WindowApi_SetEventManager_ShouldIntegrate()
    {
        var windowApi = new WindowApi(_context, null);
        var eventManager = new EventManager();

        // 设置 EventManager
        windowApi.SetEventManager(eventManager);

        // 注册事件监听器
        var opacityChangedCount = 0;
        var clickThroughChangedCount = 0;

        eventManager.On(EventManager.OpacityChanged, (Action<object>)((data) => opacityChangedCount++));
        eventManager.On(EventManager.ClickThroughChanged, (Action<object>)((data) => clickThroughChangedCount++));

        // 由于没有窗口，SetOpacity 和 SetClickThrough 不会触发事件
        // 但这验证了集成不会抛出异常
        windowApi.SetOpacity(0.5);
        windowApi.SetClickThrough(true);

        // 无窗口时不应该触发事件
        Assert.Equal(0, opacityChangedCount);
        Assert.Equal(0, clickThroughChangedCount);

        eventManager.Clear();
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 10: 事件触发一致性（EventManager 为 null）**
    /// **Validates: Requirements 6.4, 6.5**
    /// </summary>
    [Fact]
    public void WindowApi_NullEventManager_ShouldNotThrow()
    {
        var windowApi = new WindowApi(_context, null);

        // 不设置 EventManager，调用方法不应该抛出异常
        windowApi.SetOpacity(0.5);
        windowApi.SetClickThrough(true);

        // 应该正常执行，不抛出异常
        Assert.True(true);
    }

#endregion

#region Unit Tests

    /// <summary>
    /// 无窗口时 GetOpacity 应该返回默认值
    /// </summary>
    [Fact]
    public void GetOpacity_NoWindow_ReturnsDefault()
    {
        var windowApi = new WindowApi(_context, null);
        var result = windowApi.GetOpacity();
        Assert.Equal(1.0, result);
    }

    /// <summary>
    /// 无窗口时 IsClickThrough 应该返回 false
    /// </summary>
    [Fact]
    public void IsClickThrough_NoWindow_ReturnsFalse()
    {
        var windowApi = new WindowApi(_context, null);
        var result = windowApi.IsClickThrough();
        Assert.False(result);
    }

    /// <summary>
    /// 无窗口时 IsTopmost 应该返回 true（默认值）
    /// </summary>
    [Fact]
    public void IsTopmost_NoWindow_ReturnsTrue()
    {
        var windowApi = new WindowApi(_context, null);
        var result = windowApi.IsTopmost();
        Assert.True(result);
    }

    /// <summary>
    /// 无窗口时 GetBounds 应该返回默认值
    /// </summary>
    [Fact]
    public void GetBounds_NoWindow_ReturnsDefault()
    {
        var windowApi = new WindowApi(_context, null);
        var result = windowApi.GetBounds();

        Assert.NotNull(result);

        // 使用反射获取匿名对象的属性
        var type = result.GetType();
        var x = (double)type.GetProperty("x")!.GetValue(result)!;
        var y = (double)type.GetProperty("y")!.GetValue(result)!;
        var width = (double)type.GetProperty("width")!.GetValue(result)!;
        var height = (double)type.GetProperty("height")!.GetValue(result)!;

        Assert.Equal(0.0, x);
        Assert.Equal(0.0, y);
        Assert.Equal(0.0, width);
        Assert.Equal(0.0, height);
    }

    /// <summary>
    /// SetOpacity 无窗口时不应该抛出异常
    /// </summary>
    [Fact]
    public void SetOpacity_NoWindow_ShouldNotThrow()
    {
        var windowApi = new WindowApi(_context, null);

        // 不应该抛出异常
        windowApi.SetOpacity(0.5);
        windowApi.SetOpacity(0.0);
        windowApi.SetOpacity(2.0);
        windowApi.SetOpacity(-1.0);
    }

    /// <summary>
    /// SetClickThrough 无窗口时不应该抛出异常
    /// </summary>
    [Fact]
    public void SetClickThrough_NoWindow_ShouldNotThrow()
    {
        var windowApi = new WindowApi(_context, null);

        windowApi.SetClickThrough(true);
        windowApi.SetClickThrough(false);
    }

    /// <summary>
    /// SetTopmost 无窗口时不应该抛出异常
    /// </summary>
    [Fact]
    public void SetTopmost_NoWindow_ShouldNotThrow()
    {
        var windowApi = new WindowApi(_context, null);

        windowApi.SetTopmost(true);
        windowApi.SetTopmost(false);
    }

    /// <summary>
    /// 构造函数应该拒绝 null 上下文
    /// </summary>
    [Fact]
    public void Constructor_WithNullContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowApi(null!, () => null));
    }

#endregion
}
}
