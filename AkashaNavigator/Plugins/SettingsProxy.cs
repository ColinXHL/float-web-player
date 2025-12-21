using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Services;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 设置代理对象
/// 允许 JavaScript 通过属性访问方式读写插件配置
/// 支持嵌套属性访问（如 settings.display.mode）
/// </summary>
/// <remarks>
/// 设计说明：
/// - 继承 DynamicObject 实现动态属性访问
/// - 合并 default_config（来自 plugin.json）和用户配置
/// - 用户配置优先级高于默认配置
/// - 支持点号路径的嵌套属性访问
/// - 写入时自动保存到配置文件
/// </remarks>
public class SettingsProxy : DynamicObject
{
#region Fields

    private readonly PluginConfig _config;
    private readonly Dictionary<string, JsonElement>? _defaults;
    private readonly string _pluginId;
    private readonly string _basePath;

#endregion

#region Constructor

    /// <summary>
    /// 创建设置代理实例
    /// </summary>
    /// <param name="config">插件配置</param>
    /// <param name="defaults">默认配置（来自 plugin.json 的 default_config）</param>
    /// <param name="pluginId">插件 ID（用于日志）</param>
    public SettingsProxy(PluginConfig config, Dictionary<string, JsonElement>? defaults, string pluginId)
        : this(config, defaults, pluginId, string.Empty)
    {
    }

    /// <summary>
    /// 创建设置代理实例（内部构造函数，用于嵌套属性）
    /// </summary>
    private SettingsProxy(PluginConfig config, Dictionary<string, JsonElement>? defaults, string pluginId,
                          string basePath)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _defaults = defaults;
        _pluginId = pluginId;
        _basePath = basePath;
    }

#endregion

#region DynamicObject Overrides

    /// <summary>
    /// 尝试获取成员值
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var key = BuildKey(binder.Name);
        result = GetValue(key, binder.Name);
        return true; // 始终返回 true，未定义的属性返回 null/undefined
    }

    /// <summary>
    /// 尝试设置成员值
    /// </summary>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var key = BuildKey(binder.Name);
        SetValue(key, value);
        return true;
    }

    /// <summary>
    /// 获取动态成员名称列表
    /// </summary>
    public override IEnumerable<string> GetDynamicMemberNames()
    {
        var names = new HashSet<string>();

        // 从用户配置获取键
        var configKeys = GetConfigKeysAtPath(_basePath);
        foreach (var key in configKeys)
        {
            names.Add(key);
        }

        // 从默认配置获取键
        if (_defaults != null)
        {
            var prefix = string.IsNullOrEmpty(_basePath) ? "" : _basePath + ".";
            foreach (var kvp in _defaults)
            {
                if (string.IsNullOrEmpty(_basePath))
                {
                    // 顶层：获取第一级键名
                    var firstPart = kvp.Key.Split('.')[0];
                    names.Add(firstPart);
                }
                else if (kvp.Key.StartsWith(prefix))
                {
                    // 嵌套层：获取当前路径下的键名
                    var remaining = kvp.Key.Substring(prefix.Length);
                    var firstPart = remaining.Split('.')[0];
                    names.Add(firstPart);
                }
            }
        }

        return names;
    }

#endregion

#region Private Methods

    /// <summary>
    /// 构建完整的配置键路径
    /// </summary>
    private string BuildKey(string memberName)
    {
        return string.IsNullOrEmpty(_basePath) ? memberName : $"{_basePath}.{memberName}";
    }

    /// <summary>
    /// 获取配置值
    /// </summary>
    private object? GetValue(string key, string memberName)
    {
        try
        {
            // 1. 首先尝试从用户配置获取
            var userValue = _config.GetRaw(key);
            if (userValue != null)
            {
                // 如果是嵌套对象，返回新的 SettingsProxy
                if (userValue is Dictionary<string, object?> || IsNestedPath(key))
                {
                    return CreateNestedProxy(key);
                }
                return userValue;
            }

            // 2. 尝试从默认配置获取
            if (_defaults != null)
            {
                // 检查是否有精确匹配的默认值
                if (_defaults.TryGetValue(key, out var defaultElement))
                {
                    return ConvertJsonElement(defaultElement);
                }

                // 检查是否有以此键为前缀的嵌套默认值
                var prefix = key + ".";
                foreach (var kvp in _defaults)
                {
                    if (kvp.Key.StartsWith(prefix))
                    {
                        // 存在嵌套属性，返回嵌套代理
                        return CreateNestedProxy(key);
                    }
                }
            }

            // 3. 检查配置中是否存在嵌套路径
            if (HasNestedKeys(key))
            {
                return CreateNestedProxy(key);
            }

            // 4. 未找到，返回 null（JavaScript 中显示为 undefined）
            return null;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("SettingsProxy", $"GetValue({key}) failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设置配置值
    /// </summary>
    private void SetValue(string key, object? value)
    {
        try
        {
            _config.Set(key, value);
            _config.SaveToFile();
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("SettingsProxy", $"SetValue({key}) failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建嵌套属性的代理对象
    /// </summary>
    private SettingsProxy CreateNestedProxy(string path)
    {
        return new SettingsProxy(_config, _defaults, _pluginId, path);
    }

    /// <summary>
    /// 检查是否存在嵌套路径
    /// </summary>
    private bool IsNestedPath(string key)
    {
        // 检查配置中是否有以此键为前缀的其他键
        return HasNestedKeys(key);
    }

    /// <summary>
    /// 检查配置中是否存在以指定键为前缀的嵌套键
    /// </summary>
    private bool HasNestedKeys(string key)
    {
        // 检查默认配置
        if (_defaults != null)
        {
            var prefix = key + ".";
            foreach (var kvp in _defaults)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    return true;
                }
            }
        }

        // 检查用户配置（通过尝试获取嵌套值）
        var value = _config.GetRaw(key);
        return value is Dictionary<string, object?>;
    }

    /// <summary>
    /// 获取指定路径下的配置键名列表
    /// </summary>
    private IEnumerable<string> GetConfigKeysAtPath(string path)
    {
        var keys = new HashSet<string>();

        try
        {
            // 获取配置中的值
            object? value;
            if (string.IsNullOrEmpty(path))
            {
                // 顶层：获取 Settings 对象的所有键
                value = GetSettingsRootKeys();
            }
            else
            {
                value = _config.GetRaw(path);
            }

            if (value is Dictionary<string, object?> dict)
            {
                foreach (var key in dict.Keys)
                {
                    keys.Add(key);
                }
            }
            else if (value is IEnumerable<string> enumerable)
            {
                foreach (var key in enumerable)
                {
                    keys.Add(key);
                }
            }
        }
        catch
        {
            // 忽略错误
        }

        return keys;
    }

    /// <summary>
    /// 获取 Settings 根对象的所有键
    /// </summary>
    private IEnumerable<string> GetSettingsRootKeys()
    {
        var keys = new HashSet<string>();

        // 通过反射或其他方式获取 Settings 的键
        // 这里简化处理，返回空集合
        // 实际的键会从 _defaults 中获取

        return keys;
    }

    /// <summary>
    /// 将 JsonElement 转换为 .NET 原始类型
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch { JsonValueKind.String => element.GetString(),
                                          JsonValueKind.Number => ConvertJsonNumber(element),
                                          JsonValueKind.True => true,
                                          JsonValueKind.False => false,
                                          JsonValueKind.Null => null,
                                          JsonValueKind.Object => ConvertJsonObject(element),
                                          JsonValueKind.Array => ConvertJsonArray(element),
                                          _ => element.GetRawText() };
    }

    /// <summary>
    /// 将 JsonElement 数字转换为适当的 .NET 类型
    /// 优先尝试整数类型，然后是浮点类型
    /// </summary>
    private static object ConvertJsonNumber(JsonElement element)
    {
        // 首先尝试整数类型
        if (element.TryGetInt32(out var intVal))
            return intVal;
        if (element.TryGetInt64(out var longVal))
            return longVal;
        // 回退到浮点类型
        return element.GetDouble();
    }

    /// <summary>
    /// 将 JsonElement 对象转换为字典
    /// </summary>
    private static Dictionary<string, object?> ConvertJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertJsonElement(prop.Value);
        }
        return dict;
    }

    /// <summary>
    /// 将 JsonElement 数组转换为列表
    /// </summary>
    private static List<object?> ConvertJsonArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(ConvertJsonElement(item));
        }
        return list;
    }

#endregion
}
}
