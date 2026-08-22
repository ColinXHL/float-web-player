namespace AkashaNavigator.Models.PluginRepository;

/// <summary>
/// Optional catalog-side data that can move independently from plugin code.
/// </summary>
public sealed class PluginResourceCatalog
{
    public int SchemaVersion { get; set; } = 1;

    public string PluginId { get; set; } = string.Empty;

    public List<CatalogPluginResource> Resources { get; set; } = new();
}

public sealed class CatalogPluginResource
{
    public string Id { get; set; } = string.Empty;

    public string Revision { get; set; } = string.Empty;

    public string SourceVersion { get; set; } = string.Empty;

    public bool Optional { get; set; } = true;

    public CatalogPluginDistribution Distribution { get; set; } = new();
}
