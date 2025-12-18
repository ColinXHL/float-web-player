using System;
using System.IO;
using SandronePlayer.Helpers;
using SandronePlayer.Models;

namespace SandronePlayer.Services
{
    /// <summary>
    /// 窗口状态服务
    /// 负责保存和加载窗口位置、大小、最后访问 URL 等
    /// </summary>
    public class WindowStateService
    {
        #region Singleton

        private static WindowStateService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static WindowStateService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new WindowStateService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Fields

        private WindowState? _cachedState;

        #endregion

        #region Constructor

        private WindowStateService()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载窗口状态
        /// </summary>
        public WindowState Load()
        {
            if (_cachedState != null)
                return _cachedState;

            var filePath = GetFilePath();
            try
            {
                _cachedState = JsonHelper.LoadFromFile<WindowState>(filePath);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("WindowStateService", $"加载窗口状态失败 [{filePath}]: {ex.Message}");
                _cachedState = null;
            }

            // 返回默认状态
            if (_cachedState == null)
            {
                _cachedState = CreateDefaultState();
            }

            return _cachedState;
        }

        /// <summary>
        /// 保存窗口状态
        /// </summary>
        public void Save(WindowState state)
        {
            _cachedState = state;

            var filePath = GetFilePath();
            try
            {
                JsonHelper.SaveToFile(filePath, state);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("WindowStateService", $"保存窗口状态失败 [{filePath}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新并保存窗口状态
        /// </summary>
        public void Update(Action<WindowState> updateAction)
        {
            var state = Load();
            updateAction(state);
            Save(state);
        }

        #endregion

        #region Private Methods

        private string GetFilePath()
        {
            return Path.Combine(ProfileManager.Instance.GetCurrentProfileDirectory(), AppConstants.WindowStateFileName);
        }

        private WindowState CreateDefaultState()
        {
            // 获取主屏幕工作区域
            var workArea = System.Windows.SystemParameters.WorkArea;
            // 获取主屏幕完整高度（包含任务栏）
            double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;

            // 计算默认大小：宽度为屏幕的 1/4，高度按 16:9 比例计算
            double defaultWidth = Math.Max(workArea.Width / 4, AppConstants.MinWindowWidth);
            double defaultHeight = defaultWidth * 9 / 16;

            if (defaultHeight < AppConstants.MinWindowHeight)
            {
                defaultHeight = AppConstants.MinWindowHeight;
                defaultWidth = defaultHeight * 16 / 9;
            }

            return new WindowState
            {
                Left = workArea.Left,
                Top = screenHeight - defaultHeight,  // 贴到屏幕最底部（任务栏底部）
                Width = defaultWidth,
                Height = defaultHeight,
                Opacity = AppConstants.MaxOpacity,
                IsMaximized = false,
                LastUrl = AppConstants.DefaultHomeUrl,
                IsMuted = false
            };
        }

        #endregion
    }
}
