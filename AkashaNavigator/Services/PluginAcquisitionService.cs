using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// Central entry point for installing or updating a catalog plugin by ID.
/// </summary>
public sealed class PluginAcquisitionService : IPluginAcquisitionService
{
    private readonly IPluginRepositoryService _repositoryService;
    private readonly IPluginInstaller _pluginInstaller;
    private readonly IPluginLibrary _pluginLibrary;

    public PluginAcquisitionService(
        IPluginRepositoryService repositoryService,
        IPluginInstaller pluginInstaller,
        IPluginLibrary pluginLibrary)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _pluginInstaller = pluginInstaller ?? throw new ArgumentNullException(nameof(pluginInstaller));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
    }

    public async Task<Result<InstalledPluginInfo>> EnsureInstalledAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        return await EnsureInstalledAsync(pluginId, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<InstalledPluginInfo>> EnsureInstalledAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (_pluginLibrary.IsInstalled(pluginId))
        {
            var installed = _pluginLibrary.GetInstalledPluginInfo(pluginId);
            if (installed != null)
            {
                return Result<InstalledPluginInfo>.Success(installed);
            }

            return Result<InstalledPluginInfo>.Failure(
                Error.Plugin(
                    PluginErrorCodes.InvalidManifest,
                    $"已安装插件 {pluginId} 的清单不可用",
                    pluginId: pluginId));
        }

        return await InstallOrUpdateAsync(pluginId, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<InstalledPluginInfo>> InstallOrUpdateAsync(
        string pluginId,
        IProgress<PluginDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var initializeResult = await _repositoryService.InitializeAsync(cancellationToken)
            .ConfigureAwait(false);
        if (initializeResult.IsFailure)
        {
            return Result<InstalledPluginInfo>.Failure(initializeResult.Error!);
        }

        return await _pluginInstaller.InstallOrUpdateRepositoryPluginAsync(
                pluginId,
                progress,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
