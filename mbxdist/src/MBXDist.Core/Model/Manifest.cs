namespace MBXDist.Core.Model;

public sealed class Manifest
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<Dependency> Dependencies { get; set; } = new();
    public List<ResourceEntry> Resources { get; set; } = new();
}

public sealed class Dependency
{
    public string Id { get; set; } = "";
    public string MinVersion { get; set; } = "";
}

public sealed class ResourceEntry
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
    public TargetRef Target { get; set; } = new();
    public bool Locked { get; set; }

    /// <summary>True (default) = the file must carry a valid Authenticode signature from the pinned cert.
    /// False = third-party binary we do not sign (ffmpeg, essentia): integrity rides on Sha256 alone,
    /// which is trustworthy because this manifest ships inside the signed client.</summary>
    public bool Authenticode { get; set; } = true;
}

public sealed class TargetRef
{
    public string Root { get; set; } = "";
    public string Path { get; set; } = "";
}
