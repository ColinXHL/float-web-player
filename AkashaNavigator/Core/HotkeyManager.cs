using System;
using System.Collections.Generic;
using System.Linq;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Views.Windows;
using AkashaNavigator.Services;
using Serilog;
using System.Threading;
using System.Windows.Threading;

namespace AkashaNavigator.Core
{
/// <summary>
/// 全局快捷键管理器
/// 负责初始化和管理全局快捷键服务
/// </summary>
public class HotkeyManager : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", nameof(HotkeyManager));

private readonly HotkeyService _hotkeyService;
    private readonly IEventBus _eventBus;
    private PlayerWindow? _playerWindow;
    private AppConfig _config = null!;
    private Action<string, string?>? _showOsdAction;

    private DateTime _lastSeekTime = DateTime.MinValue;
    private int _pendingSeekDeltaSeconds;
    private readonly DispatcherTimer _seekFlushTimer;
    private const int SeekThrottleMs = 200;
    private readonly Dictionary<string, DateTime> _lastActionTime = new(StringComparer.OrdinalIgnoreCase);
    private const int ToggleActionDebounceMs = 180;
    private int _disposed;

    public HotkeyManager(HotkeyService hotkeyService, IEventBus eventBus)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _seekFlushTimer = new DispatcherTimer();
        _seekFlushTimer.Tick += OnSeekFlushTimerTick;
    }

    /// <summary>
    /// 初始化 HotkeyManager
    /// </summary>
    /// <param name="playerWindow">播放器窗口引用</param>
    /// <param name="config">应用配置</param>
    /// <param name="showOsdAction">显示OSD的回调</param>
public void Initialize(PlayerWindow playerWindow, AppConfig config, Action<string, string?>? showOsdAction)
    {
        _playerWindow = playerWindow;
        _config = config;
        _showOsdAction = showOsdAction;

        // 同步隐藏态热键策略到 HotkeyService
        _hotkeyService.EnableHotkeysWhenHidden = _config.EnableHotkeysWhenHidden;

        // 配置窥视按键
        _hotkeyService.SetPeekConfig(config.HotkeyPeek, config.HotkeyPeekMod, config.EnableHoldToPeek);

        // 不要替换整个配置，而是合并快捷键到现有配置：
        // 用户配置中出现的 Action 一律以用户配置为准——先移除现有配置中的旧绑定
        // （用户修改快捷键后，对应的默认快捷键不再残留生效），再添加用户绑定。
        // 插件绑定（Action 以 "Plugin:" 开头）不在用户配置中，不受影响。
        var currentConfig = _hotkeyService.GetConfig();
        var newConfig = _config.ToHotkeyConfig();

        var activeProfile = currentConfig.GetActiveProfile();
        if (activeProfile != null)
        {
            var newProfile = newConfig.GetActiveProfile();
            if (newProfile != null)
            {
                foreach (var binding in newProfile.Bindings)
                {
                    activeProfile.Bindings.RemoveAll(existing =>
                        string.Equals(existing.Action, binding.Action, StringComparison.OrdinalIgnoreCase));

                    activeProfile.Bindings.Add(binding);
                }
            }
        }

        SetupHotkeyBindings();
        _hotkeyService.Start();
    }

    /// <summary>
    /// 更新快捷键配置
    /// </summary>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        if (_hotkeyService == null)
        {
            return;
        }

        _hotkeyService.UpdateConfig(_config.ToHotkeyConfig());

        // 同步隐藏态热键策略到 HotkeyService
        _hotkeyService.EnableHotkeysWhenHidden = _config.EnableHotkeysWhenHidden;

        // 更新窥视按键配置
        _hotkeyService.SetPeekConfig(config.HotkeyPeek, config.HotkeyPeekMod, config.EnableHoldToPeek);
    }

    /// <summary>
    /// 设置快捷键事件绑定
    /// </summary>
    private void SetupHotkeyBindings()
    {
        _hotkeyService.SeekBackward += (s, e) =>
        {
            Logger.Debug("SeekBackward event received, _playerWindow is null: {IsNull}", _playerWindow == null);

            var seconds = _config.SeekSeconds;
            HandleSeekRequest(-seconds);
        };

        _hotkeyService.SeekForward += (s, e) =>
        {
            Logger.Debug("SeekForward event received, _playerWindow is null: {IsNull}", _playerWindow == null);

            var seconds = _config.SeekSeconds;
            HandleSeekRequest(seconds);
        };

        _hotkeyService.TogglePlay += (s, e) =>
        {
            if (IsActionDebounced(ActionDispatcher.ActionTogglePlay, ToggleActionDebounceMs))
                return;

            Logger.Debug("TogglePlay event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _ = _playerWindow?.TogglePlayAsync();
            ShowOsd("播放/暂停", "⏯");
        };

        _hotkeyService.DecreaseOpacity += (s, e) =>
        {
            Logger.Debug("DecreaseOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var opacity = _playerWindow?.DecreaseOpacity();
            Logger.Debug("DecreaseOpacity returned: {Opacity}", opacity);
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔅");
            }
        };

        _hotkeyService.IncreaseOpacity += (s, e) =>
        {
            Logger.Debug("IncreaseOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            var opacity = _playerWindow?.IncreaseOpacity();
            Logger.Debug("IncreaseOpacity returned: {Opacity}", opacity);
            if (opacity.HasValue)
            {
                ShowOsd($"透明度 {(int)(opacity.Value * 100)}%", "🔆");
            }
        };

        _hotkeyService.ToggleClickThrough += (s, e) =>
        {
            Logger.Debug("ToggleClickThrough event received");

            // 最大化时禁用穿透热键
            if (_playerWindow?.IsMaximized == true)
            {
                Logger.Debug("Skipped: window is maximized");
                return;
            }

            Logger.Debug("Calling ToggleClickThrough on PlayerWindow");
            var isClickThrough = _playerWindow?.ToggleClickThrough();
            Logger.Debug("ToggleClickThrough returned: {IsClickThrough}", isClickThrough);

            if (isClickThrough.HasValue)
            {
                var msg = isClickThrough.Value ? "鼠标穿透已开启" : "鼠标穿透已关闭";
                ShowOsd(msg, "👆");
            }
        };

        _hotkeyService.ToggleMaximize += (s, e) =>
        {
            if (IsActionDebounced(ActionDispatcher.ActionToggleMaximize, ToggleActionDebounceMs))
                return;

            _playerWindow?.ToggleMaximize();
            var msg = _playerWindow?.IsMaximized == true ? "窗口: 最大化" : "窗口: 还原";
            ShowOsd(msg, "🔲");
        };

        _hotkeyService.ResetOpacity += (s, e) =>
        {
            Logger.Debug("ResetOpacity event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _playerWindow?.ResetOpacity();
            ShowOsd("透明度: 100%", "🔆");
        };

        _hotkeyService.IncreasePlaybackRate += async (s, e) =>
        {
            Logger.Debug("IncreasePlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.IncreasePlaybackRateAsync();
                ShowOsd($"播放速率: {_playerWindow.CurrentPlaybackRate:F2}x", "⏩");
            }
        };

        _hotkeyService.DecreasePlaybackRate += async (s, e) =>
        {
            Logger.Debug("DecreasePlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.DecreasePlaybackRateAsync();
                ShowOsd($"播放速率: {_playerWindow.CurrentPlaybackRate:F2}x", "⏪");
            }
        };

        _hotkeyService.ResetPlaybackRate += async (s, e) =>
        {
            Logger.Debug("ResetPlaybackRate event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            if (_playerWindow != null)
            {
                await _playerWindow.ResetPlaybackRateAsync();
                ShowOsd("播放速率: 1.0x", "🔄");
            }
        };

        _hotkeyService.ToggleWindowVisibility += (s, e) =>
        {
            Logger.Debug("ToggleWindowVisibility event received, _playerWindow is null: {IsNull}", _playerWindow == null);
            _playerWindow?.ToggleVisibility();
            var isHidden = _playerWindow?.IsHidden == true;
            _hotkeyService.IsBossKeyHidden = isHidden;
            _eventBus.Publish(new BossKeyHiddenModeChangedEvent { IsHidden = isHidden });
            var msg = isHidden ? "窗口已隐藏" : "窗口已显示";
            ShowOsd(msg, "👁");
        };

        _hotkeyService.SuspendHotkeys += (s, e) =>
        {
            Logger.Debug("SuspendHotkeys event received");
            _hotkeyService.ToggleSuspend();
            var isSuspended = _hotkeyService.IsSuspended;
            var msg = isSuspended ? "热键已暂停" : "热键已恢复";
            ShowOsd(msg, isSuspended ? "⏸" : "▶");
        };

        _hotkeyService.PeekPressed += (s, e) =>
        {
            Logger.Debug("PeekPressed event received");
            _playerWindow?.SetPeekHeld(true);
        };

        _hotkeyService.PeekReleased += (s, e) =>
        {
            Logger.Debug("PeekReleased event received");
            _playerWindow?.SetPeekHeld(false);
        };
    }

    /// <summary>
    /// 显示 OSD 提示
    /// </summary>
    private void ShowOsd(string message, string? icon = null)
    {
        _showOsdAction?.Invoke(message, icon);
    }

    private bool IsActionDebounced(string actionName, int debounceMs)
    {
        var now = DateTime.Now;
        if (_lastActionTime.TryGetValue(actionName, out var lastTime))
        {
            if ((now - lastTime).TotalMilliseconds < debounceMs)
            {
                return true;
            }
        }

        _lastActionTime[actionName] = now;
        return false;
    }

    private void HandleSeekRequest(int deltaSeconds)
    {
        if (deltaSeconds == 0)
            return;

        _pendingSeekDeltaSeconds += deltaSeconds;

        var now = DateTime.Now;
        var elapsedMs = (now - _lastSeekTime).TotalMilliseconds;

        if (elapsedMs >= SeekThrottleMs)
        {
            FlushPendingSeek(now);
            return;
        }

        var remainingMs = SeekThrottleMs - elapsedMs;
        ScheduleSeekFlush(remainingMs);
    }

    private void ScheduleSeekFlush(double delayMs)
    {
        var normalizedDelayMs = Math.Max(1, (int)Math.Ceiling(delayMs));
        _seekFlushTimer.Interval = TimeSpan.FromMilliseconds(normalizedDelayMs);
        _seekFlushTimer.Stop();
        _seekFlushTimer.Start();
    }

    private void OnSeekFlushTimerTick(object? sender, EventArgs e)
    {
        _seekFlushTimer.Stop();
        FlushPendingSeek(DateTime.Now);
    }

    private void FlushPendingSeek(DateTime now)
    {
        if (_pendingSeekDeltaSeconds == 0)
            return;

        var deltaSeconds = _pendingSeekDeltaSeconds;
        _pendingSeekDeltaSeconds = 0;
        _lastSeekTime = now;

        _ = _playerWindow?.SeekAsync(deltaSeconds);

        var icon = deltaSeconds > 0 ? "⏩" : "⏪";
        var sign = deltaSeconds > 0 ? "+" : string.Empty;
        ShowOsd($"{sign}{deltaSeconds}s", icon);
    }

    /// <summary>
    /// 停止并释放快捷键服务
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _seekFlushTimer.Stop();
        _seekFlushTimer.Tick -= OnSeekFlushTimerTick;
        _hotkeyService.Dispose();
    }
}
}
