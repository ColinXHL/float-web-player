using System.Collections.Generic;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// Game Profile 配置模型
    /// 用于 JSON 序列化存储 Profile 配置
    /// </summary>
    public class GameProfile
    {
        /// <summary>
        /// Profile 唯一标识
        /// </summary>
        public string Id { get; set; } = "default";

        /// <summary>
        /// Profile 显示名称
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Profile 图标（emoji）
        /// </summary>
        public string Icon { get; set; } = "🌐";

        /// <summary>
        /// 配置版本
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 激活配置
        /// </summary>
        public ProfileActivation? Activation { get; set; }

        /// <summary>
        /// 默认设置
        /// </summary>
        public ProfileDefaults? Defaults { get; set; }

        /// <summary>
        /// 快速链接列表
        /// </summary>
        public List<QuickLink>? QuickLinks { get; set; }

        /// <summary>
        /// 外部工具列表
        /// </summary>
        public List<ExternalTool>? Tools { get; set; }

        /// <summary>
        /// 自定义脚本文件名
        /// </summary>
        public string? CustomScript { get; set; }

        /// <summary>
        /// 鼠标检测配置
        /// </summary>
        public CursorDetectionConfig? CursorDetection { get; set; }
    }

    /// <summary>
    /// Profile 激活配置
    /// </summary>
    public class ProfileActivation
    {
        /// <summary>
        /// 触发自动切换的进程名列表
        /// </summary>
        public List<string>? Processes { get; set; }

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitch { get; set; } = true;
    }

    /// <summary>
    /// Profile 默认设置
    /// </summary>
    public class ProfileDefaults
    {
        /// <summary>
        /// 默认 URL
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// 默认透明度
        /// </summary>
        public double Opacity { get; set; } = 1.0;

        /// <summary>
        /// 快进/倒退秒数
        /// </summary>
        public int SeekSeconds { get; set; } = 5;
    }

    /// <summary>
    /// 快速链接
    /// </summary>
    public class QuickLink
    {
        /// <summary>
        /// 显示标签
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// URL 地址
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// 链接类型（url/folder/action）
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// 文件/文件夹路径
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// 是否为分隔符
        /// </summary>
        public bool Separator { get; set; }
    }

    /// <summary>
    /// 外部工具
    /// </summary>
    public class ExternalTool
    {
        /// <summary>
        /// 显示标签
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 程序路径
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 启动参数
        /// </summary>
        public string? Args { get; set; }

        /// <summary>
        /// 是否以管理员身份运行
        /// </summary>
        public bool RunAsAdmin { get; set; }
    }

    /// <summary>
    /// 鼠标检测配置
    /// </summary>
    public class CursorDetectionConfig
    {
        /// <summary>
        /// 是否启用鼠标检测
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 鼠标显示时的最低透明度（0.0-1.0）
        /// </summary>
        public double MinOpacity { get; set; } = 0.3;

        /// <summary>
        /// 检测间隔（毫秒）
        /// </summary>
        public int CheckIntervalMs { get; set; } = 200;
    }
}
