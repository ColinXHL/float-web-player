using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Core;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace AkashaNavigator.Tests
{
/// <summary>
/// StorageApi 属性测试和单元测试
/// </summary>
public class StorageApiTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginContext _context;
    private readonly StorageApi _storageApi;

    public StorageApiTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"storage_api_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var manifest = new PluginManifest { Id = "test-storage-plugin", Name = "Test Storage Plugin", Version = "1.0.0",
                                            Main = "main.js", Permissions = new List<string> { "storage" } };

        // 创建必要的 JS 文件
        File.WriteAllText(Path.Combine(_tempDir, "main.js"), "function onLoad() {} function onUnload() {}");

        _context = new PluginContext(manifest, _tempDir);
        _storageApi = new StorageApi(_context);
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

#region Helper Methods

    /// <summary>
    /// 生成有效的存储键名
    /// </summary>
    private static string GenerateValidKey(int seed)
    {
        var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";
        var random = new System.Random(seed);
        var length = random.Next(1, 20);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = validChars[random.Next(validChars.Length)];
        }
        return new string(chars);
    }

    /// <summary>
    /// 检查键名是否有效
    /// </summary>
    private static bool IsValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in key)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查键名是否包含路径遍历模式
    /// </summary>
    private static bool ContainsPathTraversal(string key)
    {
        if (key.Contains("../") || key.Contains("..\\"))
            return true;
        if (key.StartsWith("..") || key.EndsWith(".."))
            return true;
        if (key == "..")
            return true;
        if (key.StartsWith("/") || key.StartsWith("\\"))
            return true;
        if (key.Length >= 2 && char.IsLetter(key[0]) && key[1] == ':')
            return true;
        return false;
    }

#endregion

#region Property 6 : 存储 Round - Trip

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 6: 存储 Round-Trip**
    /// *对于任意*有效的键名和可序列化的数据，调用 Save 后立即调用 Load 应返回等价的对象。
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StorageRoundTrip_String(PositiveInt seed, NonEmptyString value)
    {
        var key = GenerateValidKey(seed.Get);
        var data = value.Get;

        var saveResult = _storageApi.Save(key, data);
        var loadedData = _storageApi.Load(key);

        // 清理
        _storageApi.Delete(key);

        // 验证保存成功且加载的数据等价
        var loadedString = loadedData?.ToString();
        var isEquivalent = saveResult && loadedString == data;

        return isEquivalent.Label($"Key: {key}, SaveResult: {saveResult}, Original: {data}, Loaded: {loadedString}");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 6: 存储 Round-Trip（数字）**
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StorageRoundTrip_Number(PositiveInt seed, int value)
    {
        var key = GenerateValidKey(seed.Get);

        var saveResult = _storageApi.Save(key, value);
        var loadedData = _storageApi.Load(key);

        // 清理
        _storageApi.Delete(key);

        // JsonElement 需要特殊处理
        int? loadedValue = null;
        if (loadedData is JsonElement element && element.ValueKind == JsonValueKind.Number)
        {
            loadedValue = element.GetInt32();
        }

        var isEquivalent = saveResult && loadedValue == value;

        return isEquivalent.Label($"Key: {key}, SaveResult: {saveResult}, Original: {value}, Loaded: {loadedValue}");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 6: 存储 Round-Trip（对象）**
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StorageRoundTrip_Object(PositiveInt seed, NonEmptyString name, int age)
    {
        var key = GenerateValidKey(seed.Get);
        var data = new { name = name.Get, age = age };

        var saveResult = _storageApi.Save(key, data);
        var loadedData = _storageApi.Load(key);

        // 清理
        _storageApi.Delete(key);

        // 验证加载的数据
        bool isEquivalent = false;
        if (saveResult && loadedData is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            var loadedName = element.GetProperty("name").GetString();
            var loadedAge = element.GetProperty("age").GetInt32();
            isEquivalent = loadedName == name.Get && loadedAge == age;
        }

        return isEquivalent.Label($"Key: {key}, SaveResult: {saveResult}, Original: {{name:{name.Get}, age:{age}}}");
    }

#endregion

#region Property 7 : 存储删除一致性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 7: 存储删除一致性**
    /// *对于任意*已保存的键名，调用 Delete 后 Exists 应返回 false，Load 应返回 null。
    /// **Validates: Requirements 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StorageDeleteConsistency(PositiveInt seed, NonEmptyString value)
    {
        var key = GenerateValidKey(seed.Get);
        var data = value.Get;

        // 先保存数据
        var saveResult = _storageApi.Save(key, data);
        var existsBeforeDelete = _storageApi.Exists(key);

        // 删除数据
        var deleteResult = _storageApi.Delete(key);

        // 验证删除后的状态
        var existsAfterDelete = _storageApi.Exists(key);
        var loadAfterDelete = _storageApi.Load(key);

        var isConsistent =
            saveResult && existsBeforeDelete && deleteResult && !existsAfterDelete && loadAfterDelete == null;

        return isConsistent.Label(
            $"Key: {key}, SaveResult: {saveResult}, ExistsBefore: {existsBeforeDelete}, " +
            $"DeleteResult: {deleteResult}, ExistsAfter: {existsAfterDelete}, LoadAfter: {loadAfterDelete}");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 7: 存储删除一致性（删除不存在的键）**
    /// **Validates: Requirements 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StorageDeleteNonExistent(PositiveInt seed)
    {
        var key = GenerateValidKey(seed.Get);

        // 确保键不存在
        _storageApi.Delete(key);

        // 删除不存在的键应该返回 true（静默成功）
        var deleteResult = _storageApi.Delete(key);
        var existsAfterDelete = _storageApi.Exists(key);
        var loadAfterDelete = _storageApi.Load(key);

        var isConsistent = deleteResult && !existsAfterDelete && loadAfterDelete == null;

        return isConsistent.Label($"Key: {key}, DeleteResult: {deleteResult}, ExistsAfter: {existsAfterDelete}");
    }

#endregion

#region Property 8 : 存储列表完整性

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 8: 存储列表完整性**
    /// *对于任意*保存的键名集合，List 方法应返回包含所有已保存键名的数组。
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property StorageListCompleteness(PositiveInt seed)
    {
        // 生成 1-5 个唯一键名
        var keyCount = (seed.Get % 5) + 1;
        var keys = new HashSet<string>();
        for (int i = 0; i < keyCount; i++)
        {
            keys.Add(GenerateValidKey(seed.Get + i * 1000));
        }

        // 保存所有键
        foreach (var key in keys)
        {
            _storageApi.Save(key, $"value_{key}");
        }

        // 获取列表
        var listedKeys = new HashSet<string>(_storageApi.List());

        // 验证所有保存的键都在列表中
        var allKeysListed = keys.All(k => listedKeys.Contains(k));

        // 清理
        foreach (var key in keys)
        {
            _storageApi.Delete(key);
        }

        return allKeysListed.Label($"Keys: [{string.Join(",", keys)}], Listed: [{string.Join(",", listedKeys)}]");
    }

    /// <summary>
    /// **Feature: plugin-api-enhancement, Property 8: 存储列表完整性（空列表）**
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public void StorageList_EmptyStorage_ReturnsEmptyArray()
    {
        // 创建新的临时目录和 StorageApi 以确保存储为空
        var emptyTempDir = Path.Combine(Path.GetTempPath(), $"storage_empty_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(emptyTempDir);

        try
        {
            var manifest = new PluginManifest { Id = "test-empty-storage", Name = "Test Empty Storage",
                                                Version = "1.0.0", Main = "main.js" };
            File.WriteAllText(Path.Combine(emptyTempDir, "main.js"), "function onLoad() {}");

            var context = new PluginContext(manifest, emptyTempDir);
            var storageApi = new StorageApi(context);

            var list = storageApi.List();

            Assert.NotNull(list);
            Assert.Empty(list);
        }
        finally
        {
            if (Directory.Exists(emptyTempDir))
            {
                Directory.Delete(emptyTempDir, true);
            }
        }
    }

#endregion

#region Unit Tests

    /// <summary>
    /// 无效键名（空字符串）应该返回 false
    /// </summary>
    [Fact]
    public void Save_EmptyKey_ReturnsFalse()
    {
        var result = _storageApi.Save("", "test");
        Assert.False(result);
    }

    /// <summary>
    /// 无效键名（null）应该返回 false
    /// </summary>
    [Fact]
    public void Save_NullKey_ReturnsFalse()
    {
        var result = _storageApi.Save(null!, "test");
        Assert.False(result);
    }

    /// <summary>
    /// 无效键名（包含特殊字符）应该返回 false
    /// </summary>
    [Theory]
    [InlineData("test/key")]
    [InlineData("test\\key")]
    [InlineData("test:key")]
    [InlineData("test*key")]
    [InlineData("test?key")]
    [InlineData("test<key")]
    [InlineData("test>key")]
    [InlineData("test|key")]
    public void Save_InvalidKeyWithSpecialChars_ReturnsFalse(string key)
    {
        var result = _storageApi.Save(key, "test");
        Assert.False(result);
    }

    /// <summary>
    /// Load 不存在的键应该返回 null
    /// </summary>
    [Fact]
    public void Load_NonExistentKey_ReturnsNull()
    {
        var result = _storageApi.Load("non_existent_key_12345");
        Assert.Null(result);
    }

    /// <summary>
    /// Exists 对不存在的键应该返回 false
    /// </summary>
    [Fact]
    public void Exists_NonExistentKey_ReturnsFalse()
    {
        var result = _storageApi.Exists("non_existent_key_12345");
        Assert.False(result);
    }

    /// <summary>
    /// Exists 对已保存的键应该返回 true
    /// </summary>
    [Fact]
    public void Exists_SavedKey_ReturnsTrue()
    {
        var key = "test_exists_key";
        _storageApi.Save(key, "test");

        var result = _storageApi.Exists(key);

        // 清理
        _storageApi.Delete(key);

        Assert.True(result);
    }

#endregion

#region Path Traversal Validation Tests(Property 11)

    /// <summary>
    /// **Feature: plugin-api-redesign, Property 11: Storage Key Validation**
    /// 包含路径遍历模式的键名应该被拒绝
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("foo/../bar")]
    [InlineData("foo\\..\\bar")]
    [InlineData("..")]
    [InlineData("test..")]
    [InlineData("..test")]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\system32")]
    [InlineData("C:secret")]
    [InlineData("D:data")]
    public void Save_PathTraversalKey_ReturnsFalse(string key)
    {
        var result = _storageApi.Save(key, "malicious data");
        Assert.False(result);
    }

    /// <summary>
    /// **Feature: plugin-api-redesign, Property 11: Storage Key Validation**
    /// 包含路径遍历模式的键名在 Load 时应该返回 null
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("C:secret")]
    public void Load_PathTraversalKey_ReturnsNull(string key)
    {
        var result = _storageApi.Load(key);
        Assert.Null(result);
    }

    /// <summary>
    /// **Feature: plugin-api-redesign, Property 11: Storage Key Validation**
    /// 包含路径遍历模式的键名在 Exists 时应该返回 false
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("C:secret")]
    public void Exists_PathTraversalKey_ReturnsFalse(string key)
    {
        var result = _storageApi.Exists(key);
        Assert.False(result);
    }

    /// <summary>
    /// **Feature: plugin-api-redesign, Property 11: Storage Key Validation**
    /// 包含路径遍历模式的键名在 Delete 时应该返回 false
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Theory]
    [InlineData("../secret")]
    [InlineData("..\\secret")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("C:secret")]
    public void Delete_PathTraversalKey_ReturnsFalse(string key)
    {
        var result = _storageApi.Delete(key);
        Assert.False(result);
    }

    /// <summary>
    /// 有效的键名应该被接受（不包含路径遍历模式）
    /// </summary>
    [Theory]
    [InlineData("valid_key")]
    [InlineData("my-data")]
    [InlineData("settings123")]
    [InlineData("user_preferences")]
    [InlineData("cache-v1")]
    public void Save_ValidKey_ReturnsTrue(string key)
    {
        var result = _storageApi.Save(key, "test data");

        // 清理
        _storageApi.Delete(key);

        Assert.True(result);
    }

#endregion
}
}
