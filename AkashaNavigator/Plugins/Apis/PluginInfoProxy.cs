using System;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.Plugins.Apis
{
/// <summary>
/// 插件信息代理（只读）
/// 暴露 plugin.json 中的元信息给 JS
/// </summary>
public class PluginInfoProxy
{
    private readonly PluginManifest _manifest;

    public PluginInfoProxy(PluginManifest manifest)
    {
        _manifest = manifest;
    }

    /// <summary>插件 ID</summary>
    public string id => _manifest.Id ?? "";

    /// <summary>插件名称</summary>
    public string name => _manifest.Name ?? "";

    /// <summary>插件版本</summary>
    public string version => _manifest.Version ?? "";

    /// <summary>插件描述</summary>
    public string description => _manifest.Description ?? "";

    /// <summary>插件作者</summary>
    public string author => _manifest.Author ?? "";

    /// <summary>入口文件</summary>
    public string main => _manifest.Main ?? "main.js";

    /// <summary>权限列表</summary>
    public string[] permissions => _manifest.Permissions?.ToArray() ?? Array.Empty<string>();
}
}
