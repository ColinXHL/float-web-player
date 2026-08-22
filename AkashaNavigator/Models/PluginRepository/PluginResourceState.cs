namespace AkashaNavigator.Models.PluginRepository;

public sealed class PluginResourceStateDocument
{
    public int SchemaVersion { get; set; } = 1;

    public Dictionary<string, PluginResourceState> Resources { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class PluginResourceState
{
    public string Revision { get; set; } = string.Empty;

    public string SourceVersion { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed record PluginResourceUpdateResult
{
    public string PluginId { get; init; } = string.Empty;

    public string ResourceId { get; init; } = string.Empty;

    public string Revision { get; init; } = string.Empty;

    public string SourceVersion { get; init; } = string.Empty;

    public bool Updated { get; init; }

    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }
}
