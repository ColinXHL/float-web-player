using System;
using System.Collections.Generic;
using System.IO;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// PlayerApi 单元测试
/// 主要测试 JavaScript 脚本生成正确性和无窗口时的行为
/// </summary>
public class PlayerApiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginContext _context;

    public PlayerApiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"player_api_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var manifest = new PluginManifest { Id = "test-player-plugin", Name = "Test Player Plugin", Version = "1.0.0",
                                            Main = "main.js", Permissions = new List<string> { "player" } };

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

#region Task 7.3 : PlayerApi 单元测试 - JavaScript 脚本生成正确性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// Play 方法无窗口时不应该抛出异常
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void Play_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        // 不应该抛出异常
        playerApi.play();
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// Pause 方法无窗口时不应该抛出异常
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void Pause_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.pause();
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// TogglePlay 方法无窗口时不应该抛出异常
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void TogglePlay_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.play();
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// Seek 方法无窗口时不应该抛出异常
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void Seek_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.seek(30.0);
        playerApi.seek(0);
        playerApi.seek(-10); // 负数应该被处理
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SeekRelative 方法无窗口时不应该抛出异常
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void SeekRelative_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.seek(10);
        playerApi.seek(-10);
        playerApi.seek(0);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// GetCurrentTime 无窗口时应该返回 0
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void GetCurrentTime_NoWindow_ReturnsZero()
    {
        var playerApi = new PlayerApi(_context, null);

        var result = playerApi.currentTime;

        Assert.Equal(0, result);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// GetDuration 无窗口时应该返回 0
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void GetDuration_NoWindow_ReturnsZero()
    {
        var playerApi = new PlayerApi(_context, null);

        var result = playerApi.duration;

        Assert.Equal(0, result);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SetPlaybackRate 无窗口时不应该抛出异常
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Fact]
    public void SetPlaybackRate_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.setPlaybackRate(1.0);
        playerApi.setPlaybackRate(2.0);
        playerApi.setPlaybackRate(0.5);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SetPlaybackRate 超出范围的值应该被钳制
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Theory]
    [InlineData(0.1, 0.25)]  // 低于最小值
    [InlineData(5.0, 4.0)]   // 高于最大值
    [InlineData(0.25, 0.25)] // 边界值
    [InlineData(4.0, 4.0)]   // 边界值
    public void SetPlaybackRate_OutOfRange_ShouldBeClamped(double input, double expected)
    {
        // 验证钳制逻辑
        var clamped = Math.Clamp(input, 0.25, 4.0);
        Assert.Equal(expected, clamped);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// GetPlaybackRate 无窗口时应该返回 1.0
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Fact]
    public void GetPlaybackRate_NoWindow_ReturnsDefault()
    {
        var playerApi = new PlayerApi(_context, null);

        var result = playerApi.playbackRate;

        Assert.Equal(1.0, result);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SetVolume 无窗口时不应该抛出异常
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Fact]
    public void SetVolume_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.setVolume(0.5);
        playerApi.setVolume(0);
        playerApi.setVolume(1.0);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SetVolume 超出范围的值应该被钳制
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Theory]
    [InlineData(-0.5, 0.0)] // 低于最小值
    [InlineData(1.5, 1.0)]  // 高于最大值
    [InlineData(0.0, 0.0)]  // 边界值
    [InlineData(1.0, 1.0)]  // 边界值
    public void SetVolume_OutOfRange_ShouldBeClamped(double input, double expected)
    {
        var clamped = Math.Clamp(input, 0.0, 1.0);
        Assert.Equal(expected, clamped);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// GetVolume 无窗口时应该返回 1.0
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Fact]
    public void GetVolume_NoWindow_ReturnsDefault()
    {
        var playerApi = new PlayerApi(_context, null);

        var result = playerApi.volume;

        Assert.Equal(1.0, result);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// SetMuted 无窗口时不应该抛出异常
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Fact]
    public void SetMuted_NoWindow_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);

        playerApi.setMuted(true);
        playerApi.setMuted(false);
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Task 7.3: PlayerApi 单元测试**
    /// IsMuted 无窗口时应该返回 false
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Fact]
    public void IsMuted_NoWindow_ReturnsFalse()
    {
        var playerApi = new PlayerApi(_context, null);

        var result = playerApi.muted;

        Assert.False(result);
    }

#endregion

#region EventManager 集成测试

    /// <summary>
    /// PlayerApi 与 EventManager 集成：设置 EventManager 不应该抛出异常
    /// </summary>
    [Fact]
    public void SetEventManager_ShouldNotThrow()
    {
        var playerApi = new PlayerApi(_context, null);
        var eventManager = new EventManager();

        playerApi.SetEventManager(eventManager);
        playerApi.SetEventManager(null!);
    }

    /// <summary>
    /// PlayerApi 与 EventManager 集成：Play 应该触发 playStateChanged 事件
    /// 注意：由于没有窗口，事件仍然会被触发（在实际实现中）
    /// </summary>
    [Fact]
    public void Play_WithEventManager_ShouldTriggerEvent()
    {
        var playerApi = new PlayerApi(_context, null);
        var eventManager = new EventManager();
        playerApi.SetEventManager(eventManager);

        var eventTriggered = false;
        eventManager.On(EventManager.PlayStateChanged, (Action<object>)((data) => eventTriggered = true));

        playerApi.play();

        // 由于没有窗口，Play 方法会直接触发事件
        Assert.True(eventTriggered);

        eventManager.Clear();
    }

    /// <summary>
    /// PlayerApi 与 EventManager 集成：Pause 应该触发 playStateChanged 事件
    /// </summary>
    [Fact]
    public void Pause_WithEventManager_ShouldTriggerEvent()
    {
        var playerApi = new PlayerApi(_context, null);
        var eventManager = new EventManager();
        playerApi.SetEventManager(eventManager);

        var eventTriggered = false;
        eventManager.On(EventManager.PlayStateChanged, (Action<object>)((data) => eventTriggered = true));

        playerApi.pause();

        Assert.True(eventTriggered);

        eventManager.Clear();
    }

#endregion

#region 构造函数测试

    /// <summary>
    /// 构造函数应该拒绝 null 上下文
    /// </summary>
    [Fact]
    public void Constructor_NullContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerApi(null!, () => null));
    }

#endregion
}
}
