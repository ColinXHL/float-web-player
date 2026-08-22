namespace AkashaNavigator.Models.PluginRepository;

/// <summary>
/// 官方插件仓库的本地配置。
/// </summary>
public sealed class PluginRepositorySettings
{
    public int SchemaVersion { get; set; } = 1;

    public PluginRepositoryChannel SelectedChannel { get; set; } =
        PluginRepositoryChannel.GitHub;

    public string CustomUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = AppConstants.OfficialPluginRepositoryBranch;

    public bool AutoUpdateRepository { get; set; } = true;

    public bool AutoUpdateSubscribedPlugins { get; set; }

    public bool AutoUpdatePluginResources { get; set; } = true;

    public string GetSelectedUrl()
    {
        return SelectedChannel switch {
            PluginRepositoryChannel.GitHub => AppConstants.OfficialPluginRepositoryGitHubUrl,
            PluginRepositoryChannel.Cnb => AppConstants.OfficialPluginRepositoryCnbUrl,
            PluginRepositoryChannel.Custom => CustomUrl ?? string.Empty,
            _ => string.Empty
        };
    }
}
