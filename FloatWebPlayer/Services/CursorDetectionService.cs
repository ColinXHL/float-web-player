using System;
using System.Windows.Threading;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 鼠标光标检测服务
    /// 用于检测游戏内鼠标是否显示（如打开地图/菜单时）
    /// </summary>
    public class CursorDetectionService : IDisposable
    {
        #region Singleton

        private static CursorDetectionService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static CursorDetectionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new CursorDetectionService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 鼠标从隐藏变为显示时触发
        /// </summary>
        public event EventHandler? CursorShown;

        /// <summary>
        /// 鼠标从显示变为隐藏时触发
        /// </summary>
        public event EventHandler? CursorHidden;

        #endregion

        #region Fields

        private DispatcherTimer? _timer;
        private string? _targetProcessName;
        private bool _lastCursorVisible = true;
        private bool _isRunning;

        #endregion


        #region Properties

        /// <summary>
        /// 是否正在运行检测
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 当前鼠标是否可见
        /// </summary>
        public bool IsCursorCurrentlyVisible => _lastCursorVisible;

        /// <summary>
        /// 目标进程名
        /// </summary>
        public string? TargetProcessName => _targetProcessName;

        #endregion

        #region Constructor

        private CursorDetectionService()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 启动鼠标检测
        /// </summary>
        /// <param name="targetProcessName">目标进程名（不含扩展名），仅当此进程在前台时检测</param>
        /// <param name="intervalMs">检测间隔（毫秒），默认 200ms</param>
        public void Start(string? targetProcessName = null, int intervalMs = 200)
        {
            if (_isRunning)
            {
                Stop();
            }

            _targetProcessName = targetProcessName;
            _lastCursorVisible = true; // 重置状态

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(50, intervalMs))
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            _isRunning = true;
        }

        /// <summary>
        /// 停止鼠标检测
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
            _isRunning = false;
            _targetProcessName = null;
        }

        /// <summary>
        /// 更新目标进程名
        /// </summary>
        /// <param name="processName">新的目标进程名</param>
        public void SetTargetProcess(string? processName)
        {
            _targetProcessName = processName;
        }

        /// <summary>
        /// 更新检测间隔
        /// </summary>
        /// <param name="intervalMs">新的检测间隔（毫秒）</param>
        public void SetInterval(int intervalMs)
        {
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, intervalMs));
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// 定时器回调：检测鼠标状态
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // 如果指定了目标进程，检查是否在前台
            if (!string.IsNullOrEmpty(_targetProcessName))
            {
                var foregroundProcess = Win32Helper.GetForegroundWindowProcessName();
                if (!string.Equals(foregroundProcess, _targetProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    // 目标进程不在前台，不检测
                    return;
                }
            }

            // 检测鼠标是否可见
            bool cursorVisible = Win32Helper.IsCursorVisible();

            // 状态变化时触发事件
            if (cursorVisible != _lastCursorVisible)
            {
                _lastCursorVisible = cursorVisible;

                if (cursorVisible)
                {
                    CursorShown?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    CursorHidden?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
