namespace MBXDist.Core.Model;

public sealed class LocalState
{
    public int SchemaVersion { get; set; } = 1;
    public string ClientId { get; set; } = "";
    public Dictionary<string, string> TargetRoots { get; set; } = new();
    public Dictionary<string, PackageState> Packages { get; set; } = new();
    // Intent only: recovery verifies bytes against the embedded manifest again.
    public Dictionary<string, string> InProgress { get; set; } = new();
}

public sealed class PackageState
{
    public string Version { get; set; } = "";
    public string InstalledAt { get; set; } = "";
    public List<ResourceState> Resources { get; set; } = new();
}

public sealed class ResourceState
{
    public string Filename { get; set; } = "";
    public string Target { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
