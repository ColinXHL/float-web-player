using System.Threading.Tasks;

namespace AkashaNavigator.Plugins
{
/// <summary>
/// 全局便捷函数
/// 提供常用的全局函数，可直接在 JavaScript 中调用
/// </summary>
public static class GlobalFunctions
{
    /// <summary>
    /// 异步延迟指定毫秒数
    /// 在 JavaScript 中可通过 await sleep(ms) 调用
    /// </summary>
    /// <param name="milliseconds">延迟毫秒数</param>
    /// <returns>延迟结束后完成的 Task</returns>
    /// <example>
    /// JavaScript 使用示例:
    /// <code>
    /// await sleep(1000);  // 等待 1 秒
    /// </code>
    /// </example>
    public static Task Sleep(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }
}
}
