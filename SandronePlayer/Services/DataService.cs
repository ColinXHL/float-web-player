using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SandronePlayer.Helpers;
using SandronePlayer.Models;

namespace SandronePlayer.Services
{
    /// <summary>
    /// JSON 数据服务
    /// 负责历史记录和收藏夹的 CRUD 操作
    /// </summary>
    public class DataService
    {
        #region Singleton

        private static DataService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static DataService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DataService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private List<HistoryItem> _historyCache = new();
        private List<BookmarkItem> _bookmarkCache = new();
        private bool _historyCacheLoaded;
        private bool _bookmarkCacheLoaded;

        #endregion

        #region Constructor

        private DataService()
        {
            // 监听 Profile 切换，清除缓存
            ProfileManager.Instance.ProfileChanged += (s, e) =>
            {
                _historyCacheLoaded = false;
                _bookmarkCacheLoaded = false;
                _historyCache.Clear();
                _bookmarkCache.Clear();
            };
        }

        #endregion

        #region History Methods

        /// <summary>
        /// 获取所有历史记录（按访问时间降序）
        /// </summary>
        public List<HistoryItem> GetHistory()
        {
            EnsureHistoryLoaded();
            return _historyCache.OrderByDescending(h => h.VisitTime).ToList();
        }

        /// <summary>
        /// 添加或更新历史记录
        /// </summary>
        public void AddHistory(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            EnsureHistoryLoaded();

            // 查找是否已存在
            var existing = _historyCache.FirstOrDefault(h => 
                h.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // 更新访问时间和次数
                existing.VisitTime = DateTime.Now;
                existing.VisitCount++;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    existing.Title = title;
                }
            }
            else
            {
                // 添加新记录
                var newItem = new HistoryItem
                {
                    Id = _historyCache.Count > 0 ? _historyCache.Max(h => h.Id) + 1 : 1,
                    Url = url,
                    Title = string.IsNullOrWhiteSpace(title) ? url : title,
                    VisitTime = DateTime.Now,
                    VisitCount = 1
                };
                _historyCache.Add(newItem);
            }

            SaveHistory();
        }

        /// <summary>
        /// 删除历史记录
        /// </summary>
        public void DeleteHistory(int id)
        {
            EnsureHistoryLoaded();
            _historyCache.RemoveAll(h => h.Id == id);
            SaveHistory();
        }

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        public void ClearHistory()
        {
            _historyCache.Clear();
            SaveHistory();
        }

        /// <summary>
        /// 搜索历史记录
        /// </summary>
        public List<HistoryItem> SearchHistory(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return GetHistory();

            EnsureHistoryLoaded();
            return _historyCache
                .Where(h => h.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           h.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.VisitTime)
                .ToList();
        }

        #endregion

        #region Bookmark Methods

        /// <summary>
        /// 获取所有收藏夹（按排序顺序）
        /// </summary>
        public List<BookmarkItem> GetBookmarks()
        {
            EnsureBookmarksLoaded();
            return _bookmarkCache.OrderBy(b => b.SortOrder).ToList();
        }

        /// <summary>
        /// 添加收藏
        /// </summary>
        public BookmarkItem AddBookmark(string url, string title)
        {
            EnsureBookmarksLoaded();

            // 检查是否已存在
            var existing = _bookmarkCache.FirstOrDefault(b =>
                b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            // 添加新收藏
            var newItem = new BookmarkItem
            {
                Id = _bookmarkCache.Count > 0 ? _bookmarkCache.Max(b => b.Id) + 1 : 1,
                Url = url,
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                AddTime = DateTime.Now,
                SortOrder = _bookmarkCache.Count
            };
            _bookmarkCache.Add(newItem);
            SaveBookmarks();

            return newItem;
        }

        /// <summary>
        /// 删除收藏
        /// </summary>
        public void DeleteBookmark(int id)
        {
            EnsureBookmarksLoaded();
            _bookmarkCache.RemoveAll(b => b.Id == id);
            SaveBookmarks();
        }

        /// <summary>
        /// 根据 URL 删除收藏
        /// </summary>
        public void DeleteBookmarkByUrl(string url)
        {
            EnsureBookmarksLoaded();
            _bookmarkCache.RemoveAll(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            SaveBookmarks();
        }

        /// <summary>
        /// 检查 URL 是否已收藏
        /// </summary>
        public bool IsBookmarked(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            EnsureBookmarksLoaded();
            return _bookmarkCache.Any(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 搜索收藏夹
        /// </summary>
        public List<BookmarkItem> SearchBookmarks(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return GetBookmarks();

            EnsureBookmarksLoaded();
            return _bookmarkCache
                .Where(b => b.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                           b.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.SortOrder)
                .ToList();
        }

        /// <summary>
        /// 清空所有收藏
        /// </summary>
        public void ClearBookmarks()
        {
            _bookmarkCache.Clear();
            SaveBookmarks();
        }

        /// <summary>
        /// 切换收藏状态
        /// </summary>
        public bool ToggleBookmark(string url, string title)
        {
            if (IsBookmarked(url))
            {
                DeleteBookmarkByUrl(url);
                return false;
            }
            else
            {
                AddBookmark(url, title);
                return true;
            }
        }

        #endregion

        #region Private Methods

        private string GetHistoryFilePath()
        {
            return Path.Combine(ProfileManager.Instance.GetCurrentProfileDirectory(), AppConstants.HistoryFileName);
        }

        private string GetBookmarksFilePath()
        {
            return Path.Combine(ProfileManager.Instance.GetCurrentProfileDirectory(), AppConstants.BookmarksFileName);
        }

        private void EnsureHistoryLoaded()
        {
            if (_historyCacheLoaded)
                return;

            var filePath = GetHistoryFilePath();
            try
            {
                _historyCache = JsonHelper.LoadFromFile<List<HistoryItem>>(filePath) ?? new();
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("DataService", $"加载历史记录失败 [{filePath}]: {ex.Message}");
                _historyCache = new();
            }
            _historyCacheLoaded = true;
        }

        private void EnsureBookmarksLoaded()
        {
            if (_bookmarkCacheLoaded)
                return;

            var filePath = GetBookmarksFilePath();
            try
            {
                _bookmarkCache = JsonHelper.LoadFromFile<List<BookmarkItem>>(filePath) ?? new();
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("DataService", $"加载收藏夹失败 [{filePath}]: {ex.Message}");
                _bookmarkCache = new();
            }
            _bookmarkCacheLoaded = true;
        }

        private void SaveHistory()
        {
            var filePath = GetHistoryFilePath();
            try
            {
                JsonHelper.SaveToFile(filePath, _historyCache);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("DataService", $"保存历史记录失败 [{filePath}]: {ex.Message}");
            }
        }

        private void SaveBookmarks()
        {
            var filePath = GetBookmarksFilePath();
            try
            {
                JsonHelper.SaveToFile(filePath, _bookmarkCache);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("DataService", $"保存收藏夹失败 [{filePath}]: {ex.Message}");
            }
        }

        #endregion
    }
}
