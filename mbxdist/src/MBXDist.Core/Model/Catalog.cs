namespace MBXDist.Core.Model;

public sealed class Catalog
{
    public int SchemaVersion { get; set; } = 1;
    public List<CatalogEntry> Packages { get; set; } = new();
}

public sealed class CatalogEntry
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
}
