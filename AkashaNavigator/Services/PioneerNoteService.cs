using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.PioneerNote;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 开荒笔记服务
/// 负责笔记数据的 CRUD 操作
/// 参考 DataService 的实现模式
/// </summary>
public class PioneerNoteService : IPioneerNoteService
{
#region Singleton

    private static IPioneerNoteService? _instance;

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static IPioneerNoteService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PioneerNoteService(LogService.Instance, ProfileManager.Instance);
            }
            return _instance;
        }
        set => _instance = value;
    }

    /// <summary>
    /// 重置单例实例（仅用于测试）
    /// </summary>
    internal static void ResetInstance()
    {
        _instance = null;
    }

#endregion

#region Fields

    private readonly ILogService _logService;
    private readonly IProfileManager _profileManager;
    private PioneerNoteData _cache = new();
    private bool _cacheLoaded;

#endregion

#region Constructor

    private PioneerNoteService()
    {
        // 监听 Profile 切换，清除缓存（与 DataService 保持一致）
        _profileManager.ProfileChanged += (s, e) =>
        {
            _cacheLoaded = false;
            _cache = new PioneerNoteData();
        };
    }

    /// <summary>
    /// DI 容器使用的构造函数
    /// </summary>
    public PioneerNoteService(ILogService logService, IProfileManager profileManager)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

        // 监听 Profile 切换，清除缓存（与 DataService 保持一致）
        _profileManager.ProfileChanged += (s, e) =>
        {
            _cacheLoaded = false;
            _cache = new PioneerNoteData();
        };
    }

#endregion

#region Properties

    /// <summary>
    /// 当前排序方向
    /// </summary>
    public SortDirection CurrentSortOrder
    {
        get {
            EnsureLoaded();
            return _cache.SortOrder;
        }
        set {
            EnsureLoaded();
            _cache.SortOrder = value;
            Save();
        }
    }

#endregion

#region Note Item Operations

    /// <summary>
    /// 记录笔记
    /// </summary>
    /// <param name="url">页面 URL</param>
    /// <param name="title">笔记标题</param>
    /// <param name="folderId">目标目录 ID（null 表示根目录）</param>
    /// <returns>创建的笔记项</returns>
    public NoteItem RecordNote(string url, string title, string? folderId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("笔记标题不能为空", nameof(title));

        EnsureLoaded();

        // 验证目录是否存在（如果指定了目录）
        if (!string.IsNullOrEmpty(folderId) && !FolderExists(folderId))
        {
            // 目录不存在，移动到根目录
            folderId = null;
        }

        // 检查同级目录是否已存在相同标题+URL的笔记
        var duplicateExists =
            _cache.Items.Any(i => i.FolderId == folderId && i.Title == title.Trim() && i.Url == (url ?? string.Empty));

        if (duplicateExists)
        {
            throw new InvalidOperationException($"同级目录下已存在相同标题和URL的笔记：{title}");
        }

        var now = DateTime.Now;
        var item = new NoteItem { Id = Guid.NewGuid().ToString(), Url = url ?? string.Empty, Title = title.Trim(),
                                  FolderId = folderId, RecordedTime = now };

        _cache.Items.Add(item);

        // 更新父目录的修改时间
        UpdateParentFolderTime(folderId, now);

        Save();

        return item;
    }

    /// <summary>
    /// 更新笔记标题
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    /// <param name="newTitle">新标题</param>
    /// <param name="newUrl">新 URL（可选，为 null 时不更新）</param>
    public void UpdateNote(string id, string newTitle, string? newUrl = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("笔记标题不能为空", nameof(newTitle));

        EnsureLoaded();

        var item = _cache.Items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            var now = DateTime.Now;
            item.Title = newTitle.Trim();

            // 更新 URL（如果提供了新值）
            if (newUrl != null && !string.IsNullOrWhiteSpace(newUrl))
            {
                item.Url = newUrl.Trim();
            }

            item.RecordedTime = now;

            // 更新父目录的修改时间
            UpdateParentFolderTime(item.FolderId, now);

            Save();
        }
    }

    /// <summary>
    /// 删除笔记
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    public void DeleteNote(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        EnsureLoaded();

        _cache.Items.RemoveAll(i => i.Id == id);
        Save();
    }

    /// <summary>
    /// 移动笔记到指定目录
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    /// <param name="targetFolderId">目标目录 ID（null 表示根目录）</param>
    public void MoveNote(string id, string? targetFolderId)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        EnsureLoaded();

        // 验证目标目录是否存在
        if (!string.IsNullOrEmpty(targetFolderId) && !FolderExists(targetFolderId))
        {
            // 目录不存在，移动到根目录
            targetFolderId = null;
        }

        var item = _cache.Items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            var now = DateTime.Now;
            item.FolderId = targetFolderId;
            item.RecordedTime = now;

            // 更新目标目录的修改时间
            UpdateParentFolderTime(targetFolderId, now);

            Save();
        }
    }

    /// <summary>
    /// 根据 ID 获取笔记项
    /// </summary>
    /// <param name="id">笔记项 ID</param>
    /// <returns>笔记项，不存在返回 null</returns>
    public NoteItem? GetNoteById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        EnsureLoaded();
        return _cache.Items.FirstOrDefault(i => i.Id == id);
    }

#endregion

#region Folder Operations

    /// <summary>
    /// 创建目录
    /// </summary>
    /// <param name="name">目录名称</param>
    /// <param name="parentId">父目录 ID（null 表示根目录）</param>
    /// <returns>创建的目录</returns>
    public NoteFolder CreateFolder(string name, string? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("目录名称不能为空", nameof(name));

        EnsureLoaded();

        // 验证父目录是否存在（如果指定了父目录）
        if (!string.IsNullOrEmpty(parentId) && !FolderExists(parentId))
        {
            // 父目录不存在，创建在根目录
            parentId = null;
        }

        // 计算同级目录的最大排序顺序
        var maxSortOrder =
            _cache.Folders.Where(f => f.ParentId == parentId).Select(f => f.SortOrder).DefaultIfEmpty(-1).Max();

        var folder = new NoteFolder { Id = Guid.NewGuid().ToString(), Name = name.Trim(), ParentId = parentId,
                                      CreatedTime = DateTime.Now, SortOrder = maxSortOrder + 1 };

        _cache.Folders.Add(folder);
        Save();

        return folder;
    }

    /// <summary>
    /// 更新目录名称
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <param name="newName">新名称</param>
    public void UpdateFolder(string id, string newName)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("目录名称不能为空", nameof(newName));

        EnsureLoaded();

        var folder = _cache.Folders.FirstOrDefault(f => f.Id == id);
        if (folder != null)
        {
            var now = DateTime.Now;
            folder.Name = newName.Trim();
            folder.CreatedTime = now;

            // 更新父目录的修改时间
            UpdateParentFolderTime(folder.ParentId, now);

            Save();
        }
    }

    /// <summary>
    /// 删除目录
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <param name="cascade">是否级联删除子目录和笔记项</param>
    public void DeleteFolder(string id, bool cascade = true)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        EnsureLoaded();

        if (cascade)
        {
            // 递归删除所有子目录和笔记项
            DeleteFolderCascade(id);
        }
        else
        {
            // 仅删除目录，将子项移动到根目录
            // 移动子目录到根目录
            foreach (var childFolder in _cache.Folders.Where(f => f.ParentId == id))
            {
                childFolder.ParentId = null;
            }

            // 移动笔记项到根目录
            foreach (var item in _cache.Items.Where(i => i.FolderId == id))
            {
                item.FolderId = null;
            }

            // 删除目录本身
            _cache.Folders.RemoveAll(f => f.Id == id);
        }

        Save();
    }

    /// <summary>
    /// 根据 ID 获取目录
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <returns>目录，不存在返回 null</returns>
    public NoteFolder? GetFolderById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        EnsureLoaded();
        return _cache.Folders.FirstOrDefault(f => f.Id == id);
    }

    /// <summary>
    /// 检查目录是否存在
    /// </summary>
    /// <param name="id">目录 ID</param>
    /// <returns>是否存在</returns>
    public bool FolderExists(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        EnsureLoaded();
        return _cache.Folders.Any(f => f.Id == id);
    }

    /// <summary>
    /// 递归删除目录及其所有子内容
    /// </summary>
    private void DeleteFolderCascade(string folderId)
    {
        // 获取所有子目录
        var childFolders = _cache.Folders.Where(f => f.ParentId == folderId).ToList();

        // 递归删除子目录
        foreach (var childFolder in childFolders)
        {
            DeleteFolderCascade(childFolder.Id);
        }

        // 删除该目录下的所有笔记项
        _cache.Items.RemoveAll(i => i.FolderId == folderId);

        // 删除目录本身
        _cache.Folders.RemoveAll(f => f.Id == folderId);
    }

    /// <summary>
    /// 递归更新父目录的修改时间
    /// </summary>
    /// <param name="folderId">目录 ID</param>
    /// <param name="time">更新时间</param>
    private void UpdateParentFolderTime(string? folderId, DateTime time)
    {
        if (string.IsNullOrEmpty(folderId))
            return;

        var folder = _cache.Folders.FirstOrDefault(f => f.Id == folderId);
        if (folder != null)
        {
            folder.CreatedTime = time;
            // 递归更新父目录
            UpdateParentFolderTime(folder.ParentId, time);
        }
    }

#endregion

#region Query Operations

    /// <summary>
    /// 获取完整的笔记数据（包含目录和项目）
    /// </summary>
    /// <returns>笔记数据</returns>
    public PioneerNoteData GetNoteTree()
    {
        EnsureLoaded();
        return _cache;
    }

    /// <summary>
    /// 获取指定目录下的笔记项
    /// </summary>
    /// <param name="folderId">目录 ID（null 表示根目录）</param>
    /// <returns>笔记项列表</returns>
    public List<NoteItem> GetItemsByFolder(string? folderId)
    {
        EnsureLoaded();
        return _cache.Items.Where(i => i.FolderId == folderId).OrderByDescending(i => i.RecordedTime).ToList();
    }

    /// <summary>
    /// 获取指定目录下的子目录
    /// </summary>
    /// <param name="parentId">父目录 ID（null 表示根目录）</param>
    /// <returns>子目录列表</returns>
    public List<NoteFolder> GetFoldersByParent(string? parentId)
    {
        EnsureLoaded();
        return _cache.Folders.Where(f => f.ParentId == parentId).OrderBy(f => f.SortOrder).ToList();
    }

    /// <summary>
    /// 获取排序后的所有笔记项
    /// </summary>
    /// <param name="direction">排序方向</param>
    /// <returns>排序后的笔记项列表</returns>
    public List<NoteItem> GetSortedItems(SortDirection direction)
    {
        EnsureLoaded();

        return direction == SortDirection.Ascending ? _cache.Items.OrderBy(i => i.RecordedTime).ToList()
                                                    : _cache.Items.OrderByDescending(i => i.RecordedTime).ToList();
    }

    /// <summary>
    /// 搜索笔记
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <returns>匹配的笔记项列表</returns>
    public List<NoteItem> SearchNotes(string keyword)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return GetSortedItems(_cache.SortOrder);
        }

        return _cache.Items
            .Where(i => i.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        i.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.RecordedTime)
            .ToList();
    }

    /// <summary>
    /// 切换排序方向
    /// </summary>
    /// <returns>切换后的排序方向</returns>
    public SortDirection ToggleSortOrder()
    {
        EnsureLoaded();

        _cache.SortOrder =
            _cache.SortOrder == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;

        Save();
        return _cache.SortOrder;
    }

    /// <summary>
    /// 获取所有笔记项数量
    /// </summary>
    /// <returns>笔记项数量</returns>
    public int GetItemCount()
    {
        EnsureLoaded();
        return _cache.Items.Count;
    }

    /// <summary>
    /// 获取所有目录数量
    /// </summary>
    /// <returns>目录数量</returns>
    public int GetFolderCount()
    {
        EnsureLoaded();
        return _cache.Folders.Count;
    }

    /// <summary>
    /// 检查 URL 是否已记录
    /// </summary>
    /// <param name="url">要检查的 URL</param>
    /// <returns>如果 URL 已记录返回 true，否则返回 false</returns>
    public bool IsUrlRecorded(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        EnsureLoaded();
        return _cache.Items.Any(i => i.Url == url);
    }

#endregion

#region Interface Compatibility Methods (for UI code compatibility)

    /// <summary>
    /// 获取所有笔记项（按排序规则）- 别名方法
    /// </summary>
    public List<NoteItem> GetAllNotes() => GetSortedItems(_cache.SortOrder);

    /// <summary>
    /// 获取指定目录下的笔记项 - 别名方法
    /// </summary>
    public List<NoteItem> GetNotesInFolder(string? folderId) => GetItemsByFolder(folderId);

    /// <summary>
    /// 重命名文件夹 - 别名方法
    /// </summary>
    public void RenameFolder(string folderId, string newName) => UpdateFolder(folderId, newName);

    /// <summary>
    /// 删除文件夹（接口兼容版本）
    /// </summary>
    public void DeleteFolder(string folderId) => DeleteFolder(folderId, cascade: true);

    /// <summary>
    /// 获取所有文件夹 - 别名方法
    /// </summary>
    public List<NoteFolder> GetAllFolders()
    {
        EnsureLoaded();
        return new List<NoteFolder>(_cache.Folders);
    }

    /// <summary>
    /// 移动笔记项到指定文件夹 - 别名方法
    /// </summary>
    public void MoveNoteToFolder(string noteId, string? targetFolderId) => MoveNote(noteId, targetFolderId);

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void ClearAll()
    {
        EnsureLoaded();
        _cache.Items.Clear();
        _cache.Folders.Clear();
        Save();
    }

#endregion

#region Private Methods

    /// <summary>
    /// 获取笔记数据文件路径
    /// </summary>
    private string GetNoteFilePath()
    {
        return Path.Combine(_profileManager.GetCurrentProfileDirectory(), AppConstants.PioneerNotesFileName);
    }

    /// <summary>
    /// 确保数据已加载
    /// </summary>
    private void EnsureLoaded()
    {
        if (_cacheLoaded)
            return;

        var filePath = GetNoteFilePath();
        try
        {
            _cache = JsonHelper.LoadFromFile<PioneerNoteData>(filePath) ?? new PioneerNoteData();
        }
        catch (Exception ex)
        {
            _logService.Warn("PioneerNoteService", "加载笔记数据失败 [{FilePath}]: {ErrorMessage}", filePath,
                                     ex.Message);
            _cache = new PioneerNoteData();
        }
        _cacheLoaded = true;
    }

    /// <summary>
    /// 保存笔记数据
    /// </summary>
    private void Save()
    {
        var filePath = GetNoteFilePath();
        try
        {
            JsonHelper.SaveToFile(filePath, _cache);
        }
        catch (Exception ex)
        {
            _logService.Debug("PioneerNoteService", "保存笔记数据失败 [{FilePath}]: {ErrorMessage}", filePath,
                                      ex.Message);
        }
    }

#endregion
}
}
