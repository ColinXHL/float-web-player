using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AkashaNavigator.Models.Plugin
{
/// <summary>
/// 插件权限常量定义
/// </summary>
public static class PluginPermissions
{
    // 现有权限
    /// <summary>音频权限 - 访问语音识别 API</summary>
    public const string Audio = "audio";
    /// <summary>覆盖层权限 - 访问 Overlay API</summary>
    public const string Overlay = "overlay";
    /// <summary>字幕权限 - 访问字幕 API</summary>
    public const string Subtitle = "subtitle";

    // 新增权限
    /// <summary>播放器权限 - 访问 Player API 控制视频播放</summary>
    public const string Player = "player";
    /// <summary>窗口权限 - 访问 Window API 控制窗口状态</summary>
    public const string Window = "window";
    /// <summary>存储权限 - 访问 Storage API 进行数据持久化</summary>
    public const string Storage = "storage";
    /// <summary>网络权限 - 访问 Http API 发起网络请求</summary>
    public const string Network = "network";
    /// <summary>事件权限 - 访问 Event API 监听应用事件</summary>
    public const string Events = "events";

    /// <summary>
    /// 所有支持的权限列表
    /// </summary>
    public static readonly string[] AllPermissions =
        new[] { Audio, Overlay, Subtitle, Player, Window, Storage, Network, Events };

    /// <summary>
    /// 检查权限名称是否有效
    /// </summary>
    /// <param name="permission">权限名称</param>
    /// <returns>是否为有效权限</returns>
    public static bool IsValidPermission(string permission)
    {
        return AllPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 插件清单模型
/// 对应 plugin.json 文件，描述插件元数据
/// </summary>
public class PluginManifest
{
#region Required Fields

    /// <summary>
    /// 插件唯一标识
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 插件显示名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 插件版本号
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 入口文件（如 main.js）
    /// </summary>
    public string? Main { get; set; }

#endregion

#region Optional Fields

    /// <summary>
    /// 插件描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 插件作者
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 最低主程序版本要求
    /// </summary>
    public string? MinAppVersion { get; set; }

    /// <summary>
    /// 权限列表
    /// 支持的权限：audio、overlay、subtitle、player、window、storage、network、events
    /// </summary>
    public List<string>? Permissions { get; set; }

    /// <summary>
    /// 默认配置
    /// </summary>
    [JsonPropertyName("defaultConfig")]
    public Dictionary<string, JsonElement>? DefaultConfig { get; set; }

    /// <summary>
    /// ES6 模块搜索路径列表
    /// 用于 import 语句解析模块时的搜索路径
    /// 支持相对路径（相对于插件目录）和绝对路径
    /// </summary>
    /// <example>["./lib", "./node_modules"]</example>
    [JsonPropertyName("library")]
    public List<string>? Library { get; set; }

    /// <summary>
    /// HTTP 请求白名单
    /// 插件只能向白名单中的 URL 发起 HTTP 请求
    /// 支持通配符 * 进行模式匹配
    /// </summary>
    /// <example>["https://api.example.com/*", "https://cdn.example.com/assets/*"]</example>
    [JsonPropertyName("http_allowed_urls")]
    public List<string>? HttpAllowedUrls { get; set; }

#endregion

#region Validation

    /// <summary>
    /// 验证清单是否有效
    /// </summary>
    /// <returns>验证结果</returns>
    public PluginManifestValidationResult Validate()
    {
        var result = new PluginManifestValidationResult();

        if (string.IsNullOrWhiteSpace(Id))
        {
            result.AddError("id", "插件 ID 是必需字段");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            result.AddError("name", "插件名称是必需字段");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            result.AddError("version", "插件版本是必需字段");
        }

        if (string.IsNullOrWhiteSpace(Main))
        {
            result.AddError("main", "入口文件是必需字段");
        }

        return result;
    }

#endregion

#region Static Methods

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
                WriteIndented = true };

    /// <summary>
    /// 从文件加载插件清单
    /// </summary>
    /// <param name="filePath">plugin.json 文件路径</param>
    /// <returns>加载结果</returns>
    public static PluginManifestLoadResult LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return PluginManifestLoadResult.Failure($"文件不存在: {filePath}");
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }
        catch (Exception ex)
        {
            return PluginManifestLoadResult.Failure($"读取文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 JSON 字符串加载插件清单
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>加载结果</returns>
    public static PluginManifestLoadResult LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return PluginManifestLoadResult.Failure("JSON 内容为空");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
            if (manifest == null)
            {
                return PluginManifestLoadResult.Failure("JSON 反序列化返回 null");
            }

            var validation = manifest.Validate();
            if (!validation.IsValid)
            {
                return PluginManifestLoadResult.Failure(validation);
            }

            return PluginManifestLoadResult.Success(manifest);
        }
        catch (JsonException ex)
        {
            return PluginManifestLoadResult.Failure($"JSON 格式错误: {ex.Message}");
        }
    }

#endregion
}

/// <summary>
/// 插件清单验证结果
/// </summary>
public class PluginManifestValidationResult
{
    private readonly Dictionary<string, string> _errors = new();

    /// <summary>
    /// 验证是否通过
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// 错误信息字典（字段名 -> 错误消息）
    /// </summary>
    public IReadOnlyDictionary<string, string> Errors => _errors;

    /// <summary>
    /// 缺失的必需字段列表
    /// </summary>
    public IEnumerable<string> MissingFields => _errors.Keys;

    /// <summary>
    /// 添加错误
    /// </summary>
    internal void AddError(string field, string message)
    {
        _errors[field] = message;
    }

    /// <summary>
    /// 获取格式化的错误消息
    /// </summary>
    public string GetErrorMessage()
    {
        if (IsValid)
            return string.Empty;
        return string.Join("; ", _errors.Select(e => $"{e.Key}: {e.Value}"));
    }
}

/// <summary>
/// 插件清单加载结果
/// </summary>
public class PluginManifestLoadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// 加载的清单（成功时有值）
    /// </summary>
    public PluginManifest? Manifest { get; private set; }

    /// <summary>
    /// 错误消息（失败时有值）
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 验证结果（验证失败时有值）
    /// </summary>
    public PluginManifestValidationResult? ValidationResult { get; private set; }

    private PluginManifestLoadResult()
    {
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PluginManifestLoadResult Success(PluginManifest manifest)
    {
        return new PluginManifestLoadResult { IsSuccess = true, Manifest = manifest };
    }

    /// <summary>
    /// 创建失败结果（带错误消息）
    /// </summary>
    public static PluginManifestLoadResult Failure(string errorMessage)
    {
        return new PluginManifestLoadResult { IsSuccess = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// 创建失败结果（带验证结果）
    /// </summary>
    public static PluginManifestLoadResult Failure(PluginManifestValidationResult validationResult)
    {
        return new PluginManifestLoadResult { IsSuccess = false, ValidationResult = validationResult,
                                              ErrorMessage = validationResult.GetErrorMessage() };
    }
}
}
