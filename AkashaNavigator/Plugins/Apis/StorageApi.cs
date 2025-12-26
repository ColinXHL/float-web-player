using System;
using System.IO;
using System.Linq;
using AkashaNavigator.Services;
using AkashaNavigator.Plugins.Core;
using Microsoft.ClearScript;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// Storage API
/// </summary>
public class StorageApi
{
    private readonly PluginContext _context;
    private readonly string _storagePath;

    public StorageApi(PluginContext context)
    {
        _context = context;
        _storagePath = Path.Combine(context.ConfigDirectory, "storage");
        if (!Directory.Exists(_storagePath))
            Directory.CreateDirectory(_storagePath);
    }

    [ScriptMember("save")]
    public bool Save(string key, object data)
    {
        if (!IsValidKey(key))
            return false;

        try
        {
            var filePath = GetFilePath(key);
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", "Storage save failed: {ErrorMessage}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 验证键名是否有效
    /// </summary>
    private bool IsValidKey(string key)
    {
        // 验证键名不为空
        if (string.IsNullOrWhiteSpace(key))
            return false;

        // 检查键名是否包含路径遍历模式
        if (key.Contains("..") || key.Contains("/") || key.Contains("\\"))
            return false;

        // 检查键名是否以点开头或结尾
        if (key.StartsWith('.') || key.EndsWith('.'))
            return false;

        // 检查键名是否包含盘符模式（如 "C:"）
        if (key.Length >= 2 && key[1] == ':')
            return false;

        // 检查键名是否包含无效字符（路径分隔符等）
        if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return true;
    }

    [ScriptMember("load")]
    public object? Load(string key)
    {
        if (!IsValidKey(key))
            return null;

        try
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
                return null;
            var json = File.ReadAllText(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<object>(json);
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Plugin:{_context.PluginId}", "Storage load failed: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    [ScriptMember("delete")]
    public bool Delete(string key)
    {
        if (!IsValidKey(key))
            return false;

        try
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            // 删除不存在的键应该返回 true（幂等操作）
            return true;
        }
        catch
        {
            return false;
        }
    }

    [ScriptMember("exists")]
    public bool Exists(string key)
    {
        if (!IsValidKey(key))
            return false;
        return File.Exists(GetFilePath(key));
    }

    [ScriptMember("list")]
    public string[] List()
    {
        if (!Directory.Exists(_storagePath))
            return Array.Empty<string>();
        return Directory.GetFiles(_storagePath, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
    }

    private string GetFilePath(string key) => Path.Combine(_storagePath, $"{key}.json");
}
}
