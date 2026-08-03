namespace MBXDist.App;

/// <summary>Compile-time app identity + defaults. The catalog and manifests are embedded resources
/// (see Embedded/); the pinned code-signing thumbprint(s) and feed base URL are configured here and
/// overridable at runtime. NOTE: PinnedThumbprints is a PLACEHOLDER until the real code-signing
/// certificate thumbprint is baked in.</summary>
public static class AppInfo
{
    public const string Version = "0.1.0";

    /// <summary>Well-known feed base URL (raw static host). Overridable via --feed.</summary>
    public const string DefaultFeedBaseUrl = "https://raw.githubusercontent.com/halrad-com/mbxhub/main/";

    /// <summary>SHA-1 thumbprint(s) of the code-signing cert MBXDist trusts. PLACEHOLDER — replace with the real token cert thumbprint before signing/shipping.</summary>
    public static readonly string[] PinnedThumbprints = { "0000000000000000000000000000000000000000" };

    /// <summary>Resource-name tokens for the embedded feed loader.</summary>
    public const string CatalogResourceSuffix = "catalog.json";
    public const string ManifestResourcePrefix = "manifests.";

    /// <summary>Self-hosted telemetry sink URL. Null = telemetry queues locally (size-capped) and is never sent.</summary>
    public const string? TelemetrySinkUrl = null;
}
