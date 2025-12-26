using System;

namespace AkashaNavigator.Plugins.Core
{
/// <summary>
/// 插件引擎初始化选项
/// </summary>
public class PluginEngineOptions
{
    /// <summary>
    /// 当前 Profile ID
    /// </summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// 当前 Profile 名称
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// 当前 Profile 目录
    /// </summary>
    public string? ProfileDirectory { get; set; }

    /// <summary>
    /// 获取 PlayerWindow 的委托
    /// </summary>
    public Func<Views.Windows.PlayerWindow?>? GetPlayerWindow { get; set; }
}
}
