using System;
using System.Collections.Generic;
using System.Diagnostics;
using SandronePlayer.Models;
using SandronePlayer.Services;

namespace SandronePlayer.Plugins
{
    /// <summary>
    /// 字幕 API
    /// 提供插件访问视频字幕数据的功能
    /// 需要 "subtitle" 权限
    /// </summary>
    public class SubtitleApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly List<Action<SubtitleEntry?>> _subtitleChangedListeners = new();
        private readonly List<Action<SubtitleData>> _subtitleLoadedListeners = new();
        private readonly List<Action> _subtitleClearedListeners = new();
        private readonly object _lock = new();
        private bool _isSubscribed;

        #endregion

        #region Events

        /// <summary>
        /// 当前字幕变化事件
        /// </summary>
        public event EventHandler<SubtitleEntry?>? OnSubtitleChanged;

        /// <summary>
        /// 字幕数据加载完成事件
        /// </summary>
        public event EventHandler<SubtitleData>? OnSubtitleLoaded;

        /// <summary>
        /// 字幕数据清除事件
        /// </summary>
        public event EventHandler? OnSubtitleCleared;

        #endregion

        #region Constructor

        /// <summary>
        /// 创建字幕 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public SubtitleApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion


        #region Properties

        /// <summary>
        /// 检查是否有字幕数据
        /// </summary>
        public bool HasSubtitles
        {
            get
            {
                try
                {
                    var data = SubtitleService.Instance.GetSubtitleData();
                    return data != null && data.Body.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 根据时间戳获取当前字幕
        /// </summary>
        /// <param name="timeInSeconds">时间戳（秒）</param>
        /// <returns>匹配的字幕条目，无匹配返回 null</returns>
        public SubtitleEntry? GetCurrent(double timeInSeconds)
        {
            try
            {
                return SubtitleService.Instance.GetSubtitleAt(timeInSeconds);
            }
            catch (Exception ex)
            {
                Log($"获取当前字幕失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有字幕
        /// </summary>
        /// <returns>字幕条目列表，无数据返回空列表</returns>
        public IReadOnlyList<SubtitleEntry> GetAll()
        {
            try
            {
                return SubtitleService.Instance.GetAllSubtitles();
            }
            catch (Exception ex)
            {
                Log($"获取所有字幕失败: {ex.Message}");
                return Array.Empty<SubtitleEntry>();
            }
        }

        /// <summary>
        /// 监听字幕变化
        /// </summary>
        /// <param name="callback">回调函数，参数为当前字幕（可能为 null）</param>
        public void OnChanged(Action<SubtitleEntry?> callback)
        {
            if (callback == null)
                return;

            lock (_lock)
            {
                _subtitleChangedListeners.Add(callback);
                EnsureSubscribed();
            }

            Log("注册字幕变化监听");
        }

        /// <summary>
        /// 监听字幕加载
        /// </summary>
        /// <param name="callback">回调函数，参数为加载的字幕数据</param>
        public void OnLoaded(Action<SubtitleData> callback)
        {
            if (callback == null)
                return;

            lock (_lock)
            {
                _subtitleLoadedListeners.Add(callback);
                EnsureSubscribed();
            }

            Log("注册字幕加载监听");

            // 如果字幕已经加载，立即触发一次回调
            try
            {
                var existingData = SubtitleService.Instance.GetSubtitleData();
                if (existingData != null && existingData.Body.Count > 0)
                {
                    Log("字幕已存在，立即触发回调");
                    callback(existingData);
                }
            }
            catch (Exception ex)
            {
                Log($"触发已有字幕回调失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 监听字幕清除
        /// </summary>
        /// <param name="callback">回调函数</param>
        public void OnCleared(Action callback)
        {
            if (callback == null)
                return;

            lock (_lock)
            {
                _subtitleClearedListeners.Add(callback);
                EnsureSubscribed();
            }

            Log("注册字幕清除监听");
        }

        /// <summary>
        /// 移除当前插件注册的所有监听器
        /// </summary>
        public void RemoveAllListeners()
        {
            lock (_lock)
            {
                _subtitleChangedListeners.Clear();
                _subtitleLoadedListeners.Clear();
                _subtitleClearedListeners.Clear();

                // 如果没有监听器了，取消订阅事件
                if (_subtitleChangedListeners.Count == 0 &&
                    _subtitleLoadedListeners.Count == 0 &&
                    _subtitleClearedListeners.Count == 0)
                {
                    Unsubscribe();
                }
            }

            Log("已移除所有监听器");
        }

        #endregion


        #region Internal Methods

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            RemoveAllListeners();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 确保已订阅字幕服务事件
        /// </summary>
        private void EnsureSubscribed()
        {
            if (_isSubscribed)
                return;

            try
            {
                SubtitleService.Instance.SubtitleChanged += OnServiceSubtitleChanged;
                SubtitleService.Instance.SubtitleLoaded += OnServiceSubtitleLoaded;
                SubtitleService.Instance.SubtitleCleared += OnServiceSubtitleCleared;
                _isSubscribed = true;
            }
            catch (Exception ex)
            {
                Log($"订阅字幕服务事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅字幕服务事件
        /// </summary>
        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            try
            {
                SubtitleService.Instance.SubtitleChanged -= OnServiceSubtitleChanged;
                SubtitleService.Instance.SubtitleLoaded -= OnServiceSubtitleLoaded;
                SubtitleService.Instance.SubtitleCleared -= OnServiceSubtitleCleared;
                _isSubscribed = false;
            }
            catch (Exception ex)
            {
                Log($"取消订阅字幕服务事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 字幕变化事件处理
        /// </summary>
        private void OnServiceSubtitleChanged(object? sender, SubtitleEntry? e)
        {
            // 触发公开事件
            OnSubtitleChanged?.Invoke(this, e);

            // 调用监听器
            List<Action<SubtitleEntry?>> listenersCopy;
            lock (_lock)
            {
                listenersCopy = new List<Action<SubtitleEntry?>>(_subtitleChangedListeners);
            }

            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener(e);
                }
                catch (Exception ex)
                {
                    Log($"字幕变化监听器回调异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 字幕加载事件处理
        /// </summary>
        private void OnServiceSubtitleLoaded(object? sender, SubtitleData e)
        {
            // 触发公开事件
            OnSubtitleLoaded?.Invoke(this, e);

            // 调用监听器
            List<Action<SubtitleData>> listenersCopy;
            lock (_lock)
            {
                listenersCopy = new List<Action<SubtitleData>>(_subtitleLoadedListeners);
            }

            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener(e);
                }
                catch (Exception ex)
                {
                    Log($"字幕加载监听器回调异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 字幕清除事件处理
        /// </summary>
        private void OnServiceSubtitleCleared(object? sender, EventArgs e)
        {
            // 触发公开事件
            OnSubtitleCleared?.Invoke(this, EventArgs.Empty);

            // 调用监听器
            List<Action> listenersCopy;
            lock (_lock)
            {
                listenersCopy = new List<Action>(_subtitleClearedListeners);
            }

            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener();
                }
                catch (Exception ex)
                {
                    Log($"字幕清除监听器回调异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            LogService.Instance.Debug($"SubtitleApi:{_context.PluginId}", message);
        }

        #endregion
    }
}
