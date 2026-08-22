using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginResourceUpdateService
{
    Task<Result<IReadOnlyList<PluginResourceUpdateResult>>> UpdateSubscribedResourcesAsync(
        bool refreshRepository = false,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PluginResourceUpdateResult>>> UpdatePluginResourcesAsync(
        string pluginId,
        bool refreshRepository = true,
        CancellationToken cancellationToken = default);

    PluginResourceState? GetInstalledState(string pluginId, string resourceId);
}
