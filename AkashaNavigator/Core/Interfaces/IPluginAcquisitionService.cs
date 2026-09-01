using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Core.Interfaces;

/// <summary>
/// Catalog-facing application service for installing or updating a plugin by its stable ID.
/// It owns repository initialization so UI callers never depend on catalog startup order.
/// </summary>
public interface IPluginAcquisitionService
{
    Task<Result<InstalledPluginInfo>> EnsureInstalledAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<Result<InstalledPluginInfo>> EnsureInstalledAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress,
        CancellationToken cancellationToken = default);

    Task<Result<InstalledPluginInfo>> InstallOrUpdateAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
