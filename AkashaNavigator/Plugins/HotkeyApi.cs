using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 热键 API
/// 允许插件注册和管理全局热键
/// </summary>
/// <remarks>
/// 设计说明：
/// - 热键通过 ActionDispatcher 注册，与系统热键服务集成
/// - 支持修饰键组合（Ctrl, Alt, Shift）
/// - 每个插件的热键相互隔离，卸载时自动清理
/// </remarks>
public class HotkeyApi
{
#region Fields

    private readonly string _pluginId;
    private readonly Dictionary<int, HotkeyRegistration> _registrations = new();
    private readonly Dictionary<string, int> _keyComboToId = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;
    private ActionDispatcher? _dispatcher;

#endregion

#region Constructor

    /// <summary>
    /// 创建热键 API 实例
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    public HotkeyApi(string pluginId)
    {
        _pluginId = pluginId;
    }

#endregion

#region Internal Methods

    /// <summary>
    /// 设置 ActionDispatcher（由 PluginEngine 调用）
    /// </summary>
    internal void SetDispatcher(ActionDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// 清理所有注册的热键（插件卸载时调用）
    /// </summary>
    internal void Cleanup()
    {
        foreach (var registration in _registrations.Values)
        {
            _dispatcher?.UnregisterAction(registration.ActionName);
        }
        _registrations.Clear();
        _keyComboToId.Clear();
    }

#endregion

#region Public Methods

    /// <summary>
    /// 注册热键
    /// </summary>
    /// <param name="keyCombo">热键组合字符串（如 "Ctrl+Shift+A"）</param>
    /// <param name="callback">回调函数</param>
    /// <returns>注册 ID，失败返回 -1</returns>
    public int Register(string keyCombo, object callback)
    {
        if (string.IsNullOrWhiteSpace(keyCombo))
        {
            LogService.Instance.Warn($"Plugin:{_pluginId}", "HotkeyApi.Register: keyCombo is empty");
            return -1;
        }

        if (callback == null)
        {
            LogService.Instance.Warn($"Plugin:{_pluginId}", "HotkeyApi.Register: callback is null");
            return -1;
        }

        // 解析热键组合
        var parseResult = HotkeyParser.Parse(keyCombo);
        if (!parseResult.IsValid)
        {
            LogService.Instance.Warn($"Plugin:{_pluginId}",
                                     $"HotkeyApi.Register: Invalid keyCombo '{keyCombo}': {parseResult.Error}");
            return -1;
        }

        // 检查是否已注册
        var normalizedCombo = parseResult.NormalizedCombo;
        if (_keyComboToId.ContainsKey(normalizedCombo))
        {
            LogService.Instance.Warn($"Plugin:{_pluginId}",
                                     $"HotkeyApi.Register: keyCombo '{keyCombo}' already registered");
            return -1;
        }

        // 生成唯一的动作名称
        var id = _nextId++;
        var actionName = $"Plugin:{_pluginId}:Hotkey:{id}";

        // 创建回调包装器
        Action actionHandler = () =>
        {
            try
            {
                // 调用 JavaScript 回调
                if (callback is Microsoft.ClearScript.ScriptObject scriptObj)
                {
                    scriptObj.InvokeAsFunction();
                }
                else if (callback is Delegate del)
                {
                    del.DynamicInvoke();
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"Plugin:{_pluginId}", $"Hotkey callback error: {ex.Message}");
            }
        };

        // 注册到 ActionDispatcher
        _dispatcher?.RegisterAction(actionName, actionHandler);

        // 保存注册信息
        var registration = new HotkeyRegistration { Id = id, KeyCombo = normalizedCombo, ActionName = actionName,
                                                    VkCode = parseResult.VkCode, Modifiers = parseResult.Modifiers };
        _registrations[id] = registration;
        _keyComboToId[normalizedCombo] = id;

        LogService.Instance.Debug($"Plugin:{_pluginId}", $"Hotkey registered: {normalizedCombo} (id={id})");
        return id;
    }

    /// <summary>
    /// 通过热键组合注销热键
    /// </summary>
    /// <param name="keyCombo">热键组合字符串</param>
    /// <returns>是否成功</returns>
    public bool Unregister(string keyCombo)
    {
        if (string.IsNullOrWhiteSpace(keyCombo))
            return false;

        var parseResult = HotkeyParser.Parse(keyCombo);
        if (!parseResult.IsValid)
            return false;

        var normalizedCombo = parseResult.NormalizedCombo;
        if (!_keyComboToId.TryGetValue(normalizedCombo, out var id))
            return false;

        return UnregisterById(id);
    }

    /// <summary>
    /// 通过 ID 注销热键
    /// </summary>
    /// <param name="id">注册 ID</param>
    /// <returns>是否成功</returns>
    public bool UnregisterById(int id)
    {
        if (!_registrations.TryGetValue(id, out var registration))
            return false;

        // 从 ActionDispatcher 注销
        _dispatcher?.UnregisterAction(registration.ActionName);

        // 移除注册信息
        _registrations.Remove(id);
        _keyComboToId.Remove(registration.KeyCombo);

        LogService.Instance.Debug($"Plugin:{_pluginId}", $"Hotkey unregistered: {registration.KeyCombo} (id={id})");
        return true;
    }

    /// <summary>
    /// 检查热键组合是否可用（未被其他插件注册）
    /// </summary>
    /// <param name="keyCombo">热键组合字符串</param>
    /// <returns>是否可用</returns>
    public bool IsAvailable(string keyCombo)
    {
        if (string.IsNullOrWhiteSpace(keyCombo))
            return false;

        var parseResult = HotkeyParser.Parse(keyCombo);
        if (!parseResult.IsValid)
            return false;

        // 检查本插件是否已注册
        return !_keyComboToId.ContainsKey(parseResult.NormalizedCombo);
    }

#endregion

#region Nested Types

    /// <summary>
    /// 热键注册信息
    /// </summary>
    private class HotkeyRegistration
    {
        public int Id { get; set; }
        public string KeyCombo { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public uint VkCode { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }

#endregion
}

/// <summary>
/// 热键解析器
/// 将字符串格式的热键组合解析为虚拟键码和修饰键
/// </summary>
public static class HotkeyParser
{
#region Key Mappings

    /// <summary>
    /// 键名到虚拟键码的映射
    /// </summary>
    private static readonly Dictionary<string, uint> KeyNameToVkCode = new(StringComparer.OrdinalIgnoreCase) {
        // 字母键
        { "A", 0x41 },
        { "B", 0x42 },
        { "C", 0x43 },
        { "D", 0x44 },
        { "E", 0x45 },
        { "F", 0x46 },
        { "G", 0x47 },
        { "H", 0x48 },
        { "I", 0x49 },
        { "J", 0x4A },
        { "K", 0x4B },
        { "L", 0x4C },
        { "M", 0x4D },
        { "N", 0x4E },
        { "O", 0x4F },
        { "P", 0x50 },
        { "Q", 0x51 },
        { "R", 0x52 },
        { "S", 0x53 },
        { "T", 0x54 },
        { "U", 0x55 },
        { "V", 0x56 },
        { "W", 0x57 },
        { "X", 0x58 },
        { "Y", 0x59 },
        { "Z", 0x5A },

        // 数字键
        { "0", 0x30 },
        { "1", 0x31 },
        { "2", 0x32 },
        { "3", 0x33 },
        { "4", 0x34 },
        { "5", 0x35 },
        { "6", 0x36 },
        { "7", 0x37 },
        { "8", 0x38 },
        { "9", 0x39 },

        // 功能键
        { "F1", 0x70 },
        { "F2", 0x71 },
        { "F3", 0x72 },
        { "F4", 0x73 },
        { "F5", 0x74 },
        { "F6", 0x75 },
        { "F7", 0x76 },
        { "F8", 0x77 },
        { "F9", 0x78 },
        { "F10", 0x79 },
        { "F11", 0x7A },
        { "F12", 0x7B },

        // 特殊键
        { "Space", 0x20 },
        { "Enter", 0x0D },
        { "Return", 0x0D },
        { "Tab", 0x09 },
        { "Escape", 0x1B },
        { "Esc", 0x1B },
        { "Backspace", 0x08 },
        { "Delete", 0x2E },
        { "Del", 0x2E },
        { "Insert", 0x2D },
        { "Ins", 0x2D },
        { "Home", 0x24 },
        { "End", 0x23 },
        { "PageUp", 0x21 },
        { "PgUp", 0x21 },
        { "PageDown", 0x22 },
        { "PgDn", 0x22 },

        // 方向键
        { "Up", 0x26 },
        { "Down", 0x28 },
        { "Left", 0x25 },
        { "Right", 0x27 },

        // 符号键
        { "OemTilde", 0xC0 },
        { "`", 0xC0 },
        { "~", 0xC0 },
        { "OemMinus", 0xBD },
        { "-", 0xBD },
        { "OemPlus", 0xBB },
        { "=", 0xBB },
        { "OemOpenBrackets", 0xDB },
        { "[", 0xDB },
        { "OemCloseBrackets", 0xDD },
        { "]", 0xDD },
        { "OemPipe", 0xDC },
        { "\\", 0xDC },
        { "OemSemicolon", 0xBA },
        { ";", 0xBA },
        { "OemQuotes", 0xDE },
        { "'", 0xDE },
        { "OemComma", 0xBC },
        { ",", 0xBC },
        { "OemPeriod", 0xBE },
        { ".", 0xBE },
        { "OemQuestion", 0xBF },
        { "/", 0xBF },

        // 小键盘
        { "NumPad0", 0x60 },
        { "NumPad1", 0x61 },
        { "NumPad2", 0x62 },
        { "NumPad3", 0x63 },
        { "NumPad4", 0x64 },
        { "NumPad5", 0x65 },
        { "NumPad6", 0x66 },
        { "NumPad7", 0x67 },
        { "NumPad8", 0x68 },
        { "NumPad9", 0x69 },
        { "Multiply", 0x6A },
        { "Add", 0x6B },
        { "Subtract", 0x6D },
        { "Decimal", 0x6E },
        { "Divide", 0x6F },
    };

    /// <summary>
    /// 修饰键名称
    /// </summary>
    private static readonly HashSet<string> ModifierNames =
        new(StringComparer.OrdinalIgnoreCase) { "Ctrl", "Control", "Alt", "Shift", "Win", "Windows" };

#endregion

#region Public Methods

    /// <summary>
    /// 解析热键组合字符串
    /// </summary>
    /// <param name="keyCombo">热键组合（如 "Ctrl+Shift+A"）</param>
    /// <returns>解析结果</returns>
    public static HotkeyParseResult Parse(string keyCombo)
    {
        if (string.IsNullOrWhiteSpace(keyCombo))
        {
            return new HotkeyParseResult { IsValid = false, Error = "Key combo is empty" };
        }

        // 分割键名
        var parts = keyCombo.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new HotkeyParseResult { IsValid = false, Error = "No keys specified" };
        }

        var modifiers = ModifierKeys.None;
        string? baseKey = null;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // 检查是否是修饰键
            if (IsModifierKey(trimmed, out var modifier))
            {
                modifiers |= modifier;
            }
            else
            {
                // 基础键（只能有一个）
                if (baseKey != null)
                {
                    return new HotkeyParseResult { IsValid = false, Error = "Multiple base keys specified" };
                }
                baseKey = trimmed;
            }
        }

        // 必须有基础键
        if (baseKey == null)
        {
            return new HotkeyParseResult { IsValid = false, Error = "No base key specified" };
        }

        // 查找虚拟键码
        if (!KeyNameToVkCode.TryGetValue(baseKey, out var vkCode))
        {
            return new HotkeyParseResult { IsValid = false, Error = $"Unknown key: {baseKey}" };
        }

        // 生成标准化的组合字符串
        var normalizedCombo = BuildNormalizedCombo(modifiers, baseKey);

        return new HotkeyParseResult { IsValid = true, VkCode = vkCode, Modifiers = modifiers, BaseKey = baseKey,
                                       NormalizedCombo = normalizedCombo };
    }

    /// <summary>
    /// 检查是否是修饰键
    /// </summary>
    private static bool IsModifierKey(string keyName, out ModifierKeys modifier)
    {
        modifier = ModifierKeys.None;

        if (string.Equals(keyName, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(keyName, "Control", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModifierKeys.Ctrl;
            return true;
        }

        if (string.Equals(keyName, "Alt", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModifierKeys.Alt;
            return true;
        }

        if (string.Equals(keyName, "Shift", StringComparison.OrdinalIgnoreCase))
        {
            modifier = ModifierKeys.Shift;
            return true;
        }

        // Win 键暂不支持（系统保留）
        if (string.Equals(keyName, "Win", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(keyName, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            // 返回 true 但不设置修饰键，表示识别但忽略
            return true;
        }

        return false;
    }

    /// <summary>
    /// 构建标准化的热键组合字符串
    /// </summary>
    private static string BuildNormalizedCombo(ModifierKeys modifiers, string baseKey)
    {
        var parts = new List<string>();

        // 按固定顺序添加修饰键
        if (modifiers.HasFlag(ModifierKeys.Ctrl))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");

        // 添加基础键（大写）
        parts.Add(baseKey.ToUpperInvariant());

        return string.Join("+", parts);
    }

#endregion
}

/// <summary>
/// 热键解析结果
/// </summary>
public class HotkeyParseResult
{
    /// <summary>
    /// 是否解析成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息（解析失败时）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 虚拟键码
    /// </summary>
    public uint VkCode { get; set; }

    /// <summary>
    /// 修饰键
    /// </summary>
    public ModifierKeys Modifiers { get; set; }

    /// <summary>
    /// 基础键名称
    /// </summary>
    public string? BaseKey { get; set; }

    /// <summary>
    /// 标准化的热键组合字符串
    /// </summary>
    public string NormalizedCombo { get; set; } = string.Empty;
}
}
