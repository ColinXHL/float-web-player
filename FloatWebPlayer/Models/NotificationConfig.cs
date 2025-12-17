namespace FloatWebPlayer.Models;

/// <summary>
/// 通知配置数据模型，包含通知显示所需的所有信息
/// </summary>
public class NotificationConfig
{
    /// <summary>
    /// 通知消息内容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 通知标题（可选）
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 通知类型，决定视觉样式
    /// </summary>
    public NotificationType Type { get; set; } = NotificationType.Info;

    /// <summary>
    /// 通知显示持续时间（毫秒）
    /// </summary>
    public int DurationMs { get; set; } = 3000;
}
