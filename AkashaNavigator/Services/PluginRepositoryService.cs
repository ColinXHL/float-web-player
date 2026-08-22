using System.IO;
using System.Text.RegularExpressions;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;

namespace AkashaNavigator.Services;

/// <summary>
/// 官方插件仓库缓存服务。
/// </summary>
public sealed class PluginRepositoryService : IPluginRepositoryService
{
    private readonly ILogService _logService;
    private readonly IPluginRepositoryGitClient _gitClient;
    private readonly string _repositoryDirectory;
    private readonly string _settingsFilePath;
    private readonly PluginWriteCoordinator _writeCoordinator;
    private readonly object _settingsLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private PluginRepositorySnapshot? _current;
    private PluginRepositorySettings _settings;

    public PluginRepositoryService(
        ILogService logService,
        PluginWriteCoordinator writeCoordinator)
        : this(
            logService,
            new PluginRepositoryGitClient(),
            AppPaths.OfficialPluginRepositoryDirectory,
            AppPaths.PluginRepositoriesConfigFilePath,
            writeCoordinator)
    {
    }

    internal PluginRepositoryService(
        ILogService logService,
        IPluginRepositoryGitClient gitClient,
        string repositoryDirectory,
        string settingsFilePath)
        : this(
            logService,
            gitClient,
            repositoryDirectory,
            settingsFilePath,
            new PluginWriteCoordinator())
    {
    }

    private PluginRepositoryService(
        ILogService logService,
        IPluginRepositoryGitClient gitClient,
        string repositoryDirectory,
        string settingsFilePath,
        PluginWriteCoordinator writeCoordinator)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _gitClient = gitClient ?? throw new ArgumentNullException(nameof(gitClient));
        _repositoryDirectory =
            repositoryDirectory ?? throw new ArgumentNullException(nameof(repositoryDirectory));
        _settingsFilePath =
            settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
        _writeCoordinator =
            writeCoordinator ?? throw new ArgumentNullException(nameof(writeCoordinator));
        _settings = LoadSettings();
        _current = LoadSnapshot(usedCache: true).Value;
    }

    public PluginRepositorySnapshot? Current => Volatile.Read(ref _current);

    public string RepositoryDirectory => _repositoryDirectory;

    public PluginRepositorySettings Settings
    {
        get
        {
            lock (_settingsLock)
            {
                return CloneSettings(_settings);
            }
        }
    }

    public async Task<Result<PluginRepositorySnapshot>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = Current;
        if (cached != null)
        {
            return Result<PluginRepositorySnapshot>.Success(cached);
        }

        return await RefreshCoreAsync(reset: false, allowCacheFallback: false, cancellationToken);
    }

    public Task<Result<PluginRepositorySnapshot>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        return RefreshCoreAsync(reset: false, allowCacheFallback: true, cancellationToken);
    }

    public Task<Result<PluginRepositorySnapshot>> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        return RefreshCoreAsync(reset: true, allowCacheFallback: false, cancellationToken);
    }

    public Result SaveSettings(PluginRepositorySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var validation = ValidateSettings(settings);
        if (validation != null)
        {
            return Result.Failure(validation);
        }

        lock (_settingsLock)
        {
            var saveResult = JsonHelper.SaveToFile(_settingsFilePath, settings);
            if (saveResult.IsFailure)
            {
                return saveResult;
            }

            _settings = CloneSettings(settings);
        }

        return Result.Success();
    }

    private async Task<Result<PluginRepositorySnapshot>> RefreshCoreAsync(
        bool reset,
        bool allowCacheFallback,
        CancellationToken cancellationToken)
    {
        var lockTaken = false;
        var settings = Settings;
        try
        {
            await _writeLock.WaitAsync(cancellationToken);
            lockTaken = true;
            using var sharedWrite =
                await _writeCoordinator.AcquireAsync(cancellationToken);

            settings = Settings;
            var validation = ValidateSettings(settings);
            if (validation != null)
            {
                return UseCacheOrFailure(validation, allowCacheFallback);
            }

            var repositoryUrl = settings.GetSelectedUrl();
            await _gitClient.SynchronizeAsync(
                repositoryUrl,
                settings.Branch,
                _repositoryDirectory,
                reset,
                cancellationToken);

            var snapshotResult = LoadSnapshot(usedCache: false);
            if (snapshotResult.IsFailure)
            {
                return UseCacheOrFailure(snapshotResult.Error!, allowCacheFallback);
            }

            Volatile.Write(ref _current, snapshotResult.Value);
            return snapshotResult;
        }
        catch (OperationCanceledException ex)
        {
            return UseCacheOrFailure(
                Error.Network(
                    "PLUGIN_REPOSITORY_SYNC_CANCELED",
                    "插件仓库同步已取消",
                    ex,
                    settings.GetSelectedUrl()),
                allowCacheFallback);
        }
        catch (Exception ex)
        {
            return UseCacheOrFailure(
                Error.Network(
                    "PLUGIN_REPOSITORY_SYNC_FAILED",
                    ex.Message,
                    ex,
                    settings.GetSelectedUrl()),
                allowCacheFallback);
        }
        finally
        {
            if (lockTaken)
            {
                _writeLock.Release();
            }
        }
    }

    private Result<PluginRepositorySnapshot> LoadSnapshot(bool usedCache)
    {
        try
        {
            var indexPath = Path.Combine(
                _repositoryDirectory,
                AppConstants.PluginRepositoryIndexFileName);
            var loadResult = JsonHelper.LoadFromFile<PluginRepositoryIndex>(indexPath);
            if (loadResult.IsFailure)
            {
                return Result<PluginRepositorySnapshot>.Failure(loadResult.Error!);
            }

            var validation = ValidateIndex(loadResult.Value!);
            if (validation != null)
            {
                return Result<PluginRepositorySnapshot>.Failure(validation);
            }

            var catalogCommit = _gitClient.GetHeadCommit(_repositoryDirectory);
            if (!IsCommitSha(catalogCommit))
            {
                return Result<PluginRepositorySnapshot>.Failure(
                    Error.FileSystem(
                        "PLUGIN_REPOSITORY_HEAD_MISSING",
                        "插件仓库缓存没有有效的 catalog HEAD",
                        filePath: _repositoryDirectory));
            }

            return Result<PluginRepositorySnapshot>.Success(
                new PluginRepositorySnapshot(loadResult.Value!, catalogCommit!, usedCache));
        }
        catch (Exception ex)
        {
            return Result<PluginRepositorySnapshot>.Failure(
                Error.FileSystem(
                    "PLUGIN_REPOSITORY_CACHE_INVALID",
                    $"读取插件仓库缓存失败: {ex.Message}",
                    ex,
                    _repositoryDirectory));
        }
    }

    private Result<PluginRepositorySnapshot> UseCacheOrFailure(
        Error error,
        bool allowCacheFallback)
    {
        var cached = Current;
        if (!allowCacheFallback || cached == null)
        {
            return Result<PluginRepositorySnapshot>.Failure(error);
        }

        _logService.Warn(
            nameof(PluginRepositoryService),
            "插件仓库同步失败，继续使用本地缓存: {ErrorMessage}",
            error.Message);
        return Result<PluginRepositorySnapshot>.Success(
            cached with { UsedCache = true });
    }

    private PluginRepositorySettings LoadSettings()
    {
        var result = JsonHelper.LoadFromFile<PluginRepositorySettings>(_settingsFilePath);
        if (result.IsSuccess && ValidateSettings(result.Value!) == null)
        {
            return result.Value!;
        }

        return new PluginRepositorySettings();
    }

    private static Error? ValidateSettings(PluginRepositorySettings settings)
    {
        if (settings.SchemaVersion != 1)
        {
            return Error.Configuration(
                "PLUGIN_REPOSITORY_SETTINGS_VERSION_INVALID",
                $"不支持的插件仓库配置版本: {settings.SchemaVersion}");
        }

        if (!Enum.IsDefined(settings.SelectedChannel))
        {
            return Error.Configuration(
                "PLUGIN_REPOSITORY_CHANNEL_INVALID",
                $"不支持的插件仓库渠道: {settings.SelectedChannel}");
        }

        if (!string.Equals(
                settings.Branch,
                AppConstants.OfficialPluginRepositoryBranch,
                StringComparison.Ordinal))
        {
            return Error.Configuration(
                "PLUGIN_REPOSITORY_BRANCH_INVALID",
                $"官方插件仓库分支必须是 {AppConstants.OfficialPluginRepositoryBranch}");
        }

        var url = settings.GetSelectedUrl();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return Error.Configuration(
                "PLUGIN_REPOSITORY_URL_INVALID",
                "插件仓库地址必须是有效的 HTTPS URL");
        }

        return null;
    }

    private static Error? ValidateIndex(PluginRepositoryIndex index)
    {
        if (index.SchemaVersion != 1 ||
            !IsCommitSha(index.Commit) ||
            index.Plugins == null)
        {
            return Error.Serialization(
                "PLUGIN_REPOSITORY_INDEX_INVALID",
                "repo.json schemaVersion 或源提交无效");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previousId = null;
        foreach (var plugin in index.Plugins)
        {
            if (plugin == null)
            {
                return Error.Serialization(
                    "PLUGIN_REPOSITORY_ENTRY_INVALID",
                    "repo.json 包含 null 插件条目");
            }

            if (!PluginIdValidator.IsValid(plugin.Id) ||
                !seenIds.Add(plugin.Id) ||
                !string.Equals(
                    plugin.Path,
                    $"{AppConstants.PluginsDirectoryName}/{plugin.Id}",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(plugin.Name) ||
                !IsSemanticVersion(plugin.Version) ||
                string.IsNullOrWhiteSpace(plugin.Description) ||
                (plugin.DistributionType != AppConstants.PluginDistributionRepository &&
                 plugin.DistributionType != AppConstants.PluginDistributionRelease) ||
                !IsSemanticVersion(plugin.MinHostVersion) ||
                (previousId != null &&
                 string.CompareOrdinal(previousId, plugin.Id) >= 0))
            {
                return Error.Serialization(
                    "PLUGIN_REPOSITORY_ENTRY_INVALID",
                    $"repo.json 包含无效或未稳定排序的插件条目: {plugin.Id}");
            }

            previousId = plugin.Id;
        }

        return null;
    }

    private static bool IsCommitSha(string? value)
    {
        return value != null &&
               value.Length is 40 or 64 &&
               Regex.IsMatch(value, "^[0-9a-f]+$", RegexOptions.CultureInvariant);
    }

    private static bool IsSemanticVersion(string? value)
    {
        return value != null &&
               Regex.IsMatch(
                   value,
                   "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
                   RegexOptions.CultureInvariant);
    }

    private static PluginRepositorySettings CloneSettings(
        PluginRepositorySettings settings)
    {
        return new PluginRepositorySettings {
            SchemaVersion = settings.SchemaVersion,
            SelectedChannel = settings.SelectedChannel,
            CustomUrl = settings.CustomUrl ?? string.Empty,
            Branch = settings.Branch ?? string.Empty,
            AutoUpdateRepository = settings.AutoUpdateRepository,
            AutoUpdateSubscribedPlugins = settings.AutoUpdateSubscribedPlugins,
            AutoUpdatePluginResources = settings.AutoUpdatePluginResources
        };
    }
}
