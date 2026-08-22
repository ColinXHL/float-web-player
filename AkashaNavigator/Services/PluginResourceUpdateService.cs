using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.Services;

/// <summary>
/// Downloads optional, independently versioned plugin data declared by the
/// official catalog. A failed resource update never changes the active file.
/// </summary>
public sealed class PluginResourceUpdateService : IPluginResourceUpdateService
{
    private const int DownloadBufferSize = 128 * 1024;
    private const long MaximumResourceBytes = 32L * 1024 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    private readonly IPluginRepositoryService _repositoryService;
    private readonly IPluginSubscriptionService _subscriptionService;
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IDownloadSourceSelector _downloadSourceSelector;
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly string _resourceRoot;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    public PluginResourceUpdateService(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        IDownloadSourceSelector downloadSourceSelector,
        HttpClient httpClient,
        ILogService logService)
        : this(
            repositoryService,
            subscriptionService,
            pluginLibrary,
            downloadSourceSelector,
            httpClient,
            logService,
            AppPaths.PluginResourcesDirectory)
    {
    }

    internal PluginResourceUpdateService(
        IPluginRepositoryService repositoryService,
        IPluginSubscriptionService subscriptionService,
        IPluginLibrary pluginLibrary,
        IDownloadSourceSelector downloadSourceSelector,
        HttpClient httpClient,
        ILogService logService,
        string resourceRoot)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _downloadSourceSelector =
            downloadSourceSelector ?? throw new ArgumentNullException(nameof(downloadSourceSelector));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _resourceRoot = Path.GetFullPath(
            resourceRoot ?? throw new ArgumentNullException(nameof(resourceRoot)));
    }

    public async Task<Result<IReadOnlyList<PluginResourceUpdateResult>>>
        UpdateSubscribedResourcesAsync(
            bool refreshRepository = false,
            CancellationToken cancellationToken = default)
    {
        var snapshotResult = await GetSnapshotAsync(refreshRepository, cancellationToken)
            .ConfigureAwait(false);
        if (snapshotResult.IsFailure)
        {
            return Result<IReadOnlyList<PluginResourceUpdateResult>>.Failure(
                snapshotResult.Error!);
        }

        var installed = _pluginLibrary.GetInstalledPlugins()
            .Select(plugin => plugin.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pluginIds = _subscriptionService.GetSubscriptions()
            .Where(
                subscription =>
                    subscription.IsAvailable &&
                    PluginIdValidator.IsValid(subscription.PluginId) &&
                    installed.Contains(subscription.PluginId))
            .Select(subscription => subscription.PluginId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pluginId => pluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<PluginResourceUpdateResult>();
        await _updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var pluginId in pluginIds)
            {
                results.AddRange(
                    await UpdatePluginCoreAsync(
                            pluginId,
                            snapshotResult.Value!,
                            cancellationToken)
                        .ConfigureAwait(false));
            }
        }
        finally
        {
            _updateLock.Release();
        }

        return Result<IReadOnlyList<PluginResourceUpdateResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<PluginResourceUpdateResult>>>
        UpdatePluginResourcesAsync(
            string pluginId,
            bool refreshRepository = true,
            CancellationToken cancellationToken = default)
    {
        if (!PluginIdValidator.IsValid(pluginId))
        {
            return Result<IReadOnlyList<PluginResourceUpdateResult>>.Failure(
                Error.Validation("PLUGIN_RESOURCE_PLUGIN_ID_INVALID", "插件 ID 无效"));
        }

        if (!_subscriptionService.IsSubscribed(pluginId) ||
            _pluginLibrary.GetInstalledPluginInfo(pluginId) == null)
        {
            return Result<IReadOnlyList<PluginResourceUpdateResult>>.Failure(
                Error.BusinessLogic(
                    "PLUGIN_RESOURCE_NOT_SUBSCRIBED",
                    "仅已安装并订阅的插件可以更新独立资源"));
        }

        var snapshotResult = await GetSnapshotAsync(refreshRepository, cancellationToken)
            .ConfigureAwait(false);
        if (snapshotResult.IsFailure)
        {
            return Result<IReadOnlyList<PluginResourceUpdateResult>>.Failure(
                snapshotResult.Error!);
        }

        await _updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = await UpdatePluginCoreAsync(
                    pluginId,
                    snapshotResult.Value!,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result<IReadOnlyList<PluginResourceUpdateResult>>.Success(results);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public PluginResourceState? GetInstalledState(string pluginId, string resourceId)
    {
        if (!PluginIdValidator.IsValid(pluginId) || !PluginIdValidator.IsValid(resourceId))
        {
            return null;
        }

        var state = LoadState(pluginId);
        return state.Resources.TryGetValue(resourceId, out var value)
            ? Clone(value)
            : null;
    }

    private async Task<Result<PluginRepositorySnapshot>> GetSnapshotAsync(
        bool refreshRepository,
        CancellationToken cancellationToken)
    {
        return refreshRepository
            ? await _repositoryService.RefreshAsync(cancellationToken).ConfigureAwait(false)
            : await _repositoryService.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PluginResourceUpdateResult>> UpdatePluginCoreAsync(
        string pluginId,
        PluginRepositorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var entry = snapshot.Index.Plugins.FirstOrDefault(
            candidate => string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return
            [
                Failed(pluginId, string.Empty, string.Empty, string.Empty, "catalog 中不存在该插件")
            ];
        }

        var expectedRepositoryPath = $"plugins/{pluginId}";
        if (!string.Equals(entry.Path, expectedRepositoryPath, StringComparison.Ordinal))
        {
            return
            [
                Failed(pluginId, string.Empty, string.Empty, string.Empty, "catalog 插件路径无效")
            ];
        }

        var catalogPath = Path.Combine(
            _repositoryService.RepositoryDirectory,
            entry.Path,
            AppConstants.PluginRepositoryResourcesFileName);
        if (!File.Exists(catalogPath))
        {
            return [];
        }

        var catalogResult = JsonHelper.LoadFromFile<PluginResourceCatalog>(catalogPath);
        if (catalogResult.IsFailure)
        {
            return
            [
                Failed(
                    pluginId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    catalogResult.Error?.Message ?? "资源清单读取失败")
            ];
        }

        if (catalogResult.Value!.SchemaVersion != 1 ||
            !string.Equals(catalogResult.Value.PluginId, pluginId, StringComparison.Ordinal))
        {
            return
            [
                Failed(pluginId, string.Empty, string.Empty, string.Empty, "资源清单身份无效")
            ];
        }

        var resources = catalogResult.Value.Resources ?? [];
        var validationError = ValidateResources(resources);
        if (validationError != null)
        {
            return
            [
                Failed(pluginId, string.Empty, string.Empty, string.Empty, validationError)
            ];
        }

        var state = LoadState(pluginId);
        var results = new List<PluginResourceUpdateResult>();
        foreach (var resource in resources.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentPath = GetCurrentPath(pluginId, resource);
            if (state.Resources.TryGetValue(resource.Id, out var currentState) &&
                await MatchesCurrentAsync(currentPath, currentState, resource, cancellationToken)
                    .ConfigureAwait(false))
            {
                results.Add(Succeeded(pluginId, resource, updated: false));
                continue;
            }

            try
            {
                await DownloadAndInstallAsync(pluginId, resource, currentPath, cancellationToken)
                    .ConfigureAwait(false);
                state.Resources[resource.Id] = new PluginResourceState {
                    Revision = resource.Revision,
                    SourceVersion = resource.SourceVersion,
                    Sha256 = resource.Distribution.Sha256!,
                    Size = resource.Distribution.Size!.Value,
                    FileName = Path.GetFileName(currentPath),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                SaveStateAtomic(pluginId, state);
                DeleteSupersededResourceFiles(pluginId, resource.Id, currentPath);
                results.Add(Succeeded(pluginId, resource, updated: true));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.Warn(
                    nameof(PluginResourceUpdateService),
                    "更新插件资源失败 ({PluginId}/{ResourceId}): {ErrorMessage}",
                    pluginId,
                    resource.Id,
                    ex.Message);
                results.Add(
                    Failed(
                        pluginId,
                        resource.Id,
                        resource.Revision,
                        resource.SourceVersion,
                        ex.Message));
            }
        }

        return results;
    }

    private async Task DownloadAndInstallAsync(
        string pluginId,
        CatalogPluginResource resource,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var package = CreatePackage(resource);
        var sourcesResult = await _downloadSourceSelector
            .GetOrderedSourcesAsync(package, cancellationToken)
            .ConfigureAwait(false);
        if (sourcesResult.IsFailure)
        {
            throw new InvalidOperationException(
                sourcesResult.Error?.Message ?? "没有可用的资源下载源");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var errors = new List<string>();
        foreach (var source in sourcesResult.Value!)
        {
            var temporaryPath = Path.Combine(
                Path.GetDirectoryName(destinationPath)!,
                $".download-{Guid.NewGuid():N}.tmp");
            try
            {
                await DownloadVerifiedAsync(source, package, temporaryPath, cancellationToken)
                    .ConfigureAwait(false);
                File.Move(temporaryPath, destinationPath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                TryDelete(temporaryPath);
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{source.Id}: {ex.Message}");
                TryDelete(temporaryPath);
            }
        }

        throw new InvalidOperationException($"所有下载源均失败：{string.Join("；", errors)}");
    }

    private async Task DownloadVerifiedAsync(
        DownloadSourceInfo source,
        PluginPackageInfo package,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        try
        {
            using var response = await _httpClient.GetAsync(
                source.Url,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var output = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                DownloadBufferSize,
                useAsync: true);

            var buffer = new byte[DownloadBufferSize];
            long received = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), timeout.Token);
                if (read == 0)
                {
                    break;
                }

                received += read;
                if (received > package.Size)
                {
                    throw new InvalidDataException("资源文件超过清单声明的大小");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }

            await output.FlushAsync(timeout.Token);
            if (received != package.Size)
            {
                throw new InvalidDataException(
                    $"资源大小不一致，预期 {package.Size} 字节，实际 {received} 字节");
            }

            output.Position = 0;
            var digest = await SHA256.HashDataAsync(output, timeout.Token);
            var actual = Convert.ToHexString(digest).ToLowerInvariant();
            if (!string.Equals(actual, package.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("资源 SHA-256 校验不一致");
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"下载源 {source.Id} 超时", ex);
        }
    }

    private static async Task<bool> MatchesCurrentAsync(
        string filePath,
        PluginResourceState state,
        CatalogPluginResource resource,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath) ||
            !string.Equals(state.Revision, resource.Revision, StringComparison.Ordinal) ||
            !string.Equals(state.SourceVersion, resource.SourceVersion, StringComparison.Ordinal) ||
            !string.Equals(state.Sha256, resource.Distribution.Sha256, StringComparison.Ordinal) ||
            state.Size != resource.Distribution.Size ||
            !string.Equals(
                state.FileName,
                Path.GetFileName(filePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var info = new FileInfo(filePath);
        if (info.Length != resource.Distribution.Size)
        {
            return false;
        }

        await using var stream = File.OpenRead(filePath);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return string.Equals(
            Convert.ToHexString(digest).ToLowerInvariant(),
            resource.Distribution.Sha256,
            StringComparison.Ordinal);
    }

    private static string? ValidateResources(IReadOnlyList<CatalogPluginResource> resources)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            var distribution = resource.Distribution;
            if (!PluginIdValidator.IsValid(resource.Id) || !ids.Add(resource.Id))
            {
                return "资源 ID 无效或重复";
            }

            if (string.IsNullOrWhiteSpace(resource.Revision) || resource.Revision.Length > 128 ||
                string.IsNullOrWhiteSpace(resource.SourceVersion) || resource.SourceVersion.Length > 128 ||
                distribution == null ||
                !string.Equals(distribution.Type, AppConstants.PluginDistributionRelease, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(distribution.Tag) || distribution.Tag.Length > 200 ||
                string.IsNullOrWhiteSpace(distribution.Asset) ||
                !string.Equals(Path.GetFileName(distribution.Asset), distribution.Asset, StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(distribution.Asset), ".json", StringComparison.OrdinalIgnoreCase) ||
                distribution.Size is null or <= 0 or > MaximumResourceBytes ||
                !IsSha256(distribution.Sha256) ||
                !string.Equals(resource.Revision, distribution.Sha256, StringComparison.Ordinal))
            {
                return $"资源 {resource.Id} 的 Release 元数据无效";
            }
        }

        return null;
    }

    private PluginResourceStateDocument LoadState(string pluginId)
    {
        var path = GetStatePath(pluginId);
        if (!File.Exists(path))
        {
            return new PluginResourceStateDocument();
        }

        try
        {
            var state = JsonSerializer.Deserialize<PluginResourceStateDocument>(
                File.ReadAllText(path),
                JsonHelper.ReadOptions);
            return state is { SchemaVersion: 1, Resources: not null }
                ? state
                : new PluginResourceStateDocument();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.Warn(
                nameof(PluginResourceUpdateService),
                "插件资源状态不可用，将重新验证资源 ({PluginId}): {ErrorMessage}",
                pluginId,
                ex.Message);
            return new PluginResourceStateDocument();
        }
    }

    private void SaveStateAtomic(string pluginId, PluginResourceStateDocument state)
    {
        var path = GetStatePath(pluginId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonHelper.Serialize(state));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private PluginPackageInfo CreatePackage(CatalogPluginResource resource)
    {
        var tag = Uri.EscapeDataString(resource.Distribution.Tag!);
        var asset = Uri.EscapeDataString(resource.Distribution.Asset!);
        return new PluginPackageInfo {
            FileName = resource.Distribution.Asset!,
            Size = resource.Distribution.Size!.Value,
            Sha256 = resource.Distribution.Sha256!,
            Sources =
            [
                new DownloadSourceInfo {
                    Id = "github",
                    Url = string.Format(AppConstants.OfficialPluginReleaseGitHubUrlFormat, tag, asset)
                },
                new DownloadSourceInfo {
                    Id = "cnb",
                    Url = string.Format(AppConstants.OfficialPluginReleaseCnbUrlFormat, tag, asset)
                }
            ]
        };
    }

    private string GetCurrentPath(string pluginId, CatalogPluginResource resource)
    {
        var extension = Path.GetExtension(resource.Distribution.Asset!);
        return Path.Combine(
            GetPluginRoot(pluginId),
            resource.Id,
            $"{resource.Distribution.Sha256}{extension.ToLowerInvariant()}");
    }

    private void DeleteSupersededResourceFiles(
        string pluginId,
        string resourceId,
        string currentPath)
    {
        var directory = Path.Combine(GetPluginRoot(pluginId), resourceId);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (!string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(path);
            }
        }
    }

    private string GetStatePath(string pluginId) =>
        Path.Combine(GetPluginRoot(pluginId), "resource-state.json");

    private string GetPluginRoot(string pluginId) =>
        Path.Combine(_resourceRoot, pluginId);

    private static PluginResourceState Clone(PluginResourceState state) =>
        new() {
            Revision = state.Revision,
            SourceVersion = state.SourceVersion,
            Sha256 = state.Sha256,
            Size = state.Size,
            FileName = state.FileName,
            UpdatedAtUtc = state.UpdatedAtUtc
        };

    private static PluginResourceUpdateResult Succeeded(
        string pluginId,
        CatalogPluginResource resource,
        bool updated) =>
        new() {
            PluginId = pluginId,
            ResourceId = resource.Id,
            Revision = resource.Revision,
            SourceVersion = resource.SourceVersion,
            Updated = updated,
            Succeeded = true
        };

    private static PluginResourceUpdateResult Failed(
        string pluginId,
        string resourceId,
        string revision,
        string sourceVersion,
        string message) =>
        new() {
            PluginId = pluginId,
            ResourceId = resourceId,
            Revision = revision,
            SourceVersion = sourceVersion,
            Succeeded = false,
            ErrorMessage = message
        };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
