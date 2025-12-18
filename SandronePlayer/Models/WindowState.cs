namespace SandronePlayer.Models
{
    /// <summary>
    /// 窗口状态模型
    /// 用于 JSON 序列化保存窗口位置和大小（Phase 15）
    /// </summary>
    public class WindowState
    {
        /// <summary>
        /// 窗口左边位置
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// 窗口顶部位置
        /// </summary>
        public double Top { get; set; }

        /// <summary>
        /// 窗口宽度
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 窗口高度
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 窗口透明度
        /// </summary>
        public double Opacity { get; set; } = AppConstants.MaxOpacity;

        /// <summary>
        /// 是否最大化
        /// </summary>
        public bool IsMaximized { get; set; }

        /// <summary>
        /// 最后访问的 URL
        /// </summary>
        public string? LastUrl { get; set; }

        /// <summary>
        /// 是否静音
        /// </summary>
        public bool IsMuted { get; set; }
    }
}
