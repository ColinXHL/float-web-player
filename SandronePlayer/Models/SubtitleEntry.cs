namespace SandronePlayer.Models
{
    /// <summary>
    /// 单条字幕记录
    /// 包含开始时间、结束时间和文本内容
    /// </summary>
    public class SubtitleEntry
    {
        /// <summary>
        /// 开始时间（秒）
        /// </summary>
        public double From { get; set; }

        /// <summary>
        /// 结束时间（秒）
        /// </summary>
        public double To { get; set; }

        /// <summary>
        /// 字幕文本内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 检查给定时间是否在此字幕的时间范围内
        /// 时间范围为 [From, To)，即包含开始时间，不包含结束时间
        /// </summary>
        /// <param name="timeInSeconds">要检查的时间（秒）</param>
        /// <returns>如果时间在范围内返回 true，否则返回 false</returns>
        public bool ContainsTime(double timeInSeconds)
        {
            return timeInSeconds >= From && timeInSeconds < To;
        }
    }
}
