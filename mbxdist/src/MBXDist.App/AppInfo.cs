namespace MBXDist.App;

/// <summary>Compile-time app identity + defaults. The catalog and manifests are embedded resources
/// (see Embedded/). The feed URL is overridable; certificate pins are compile-time trust configuration.</summary>
public static class AppInfo
{
    public const string Version = "0.1.0";

    /// <summary>Well-known feed base URL (raw static host). Overridable via --feed.</summary>
    public const string DefaultFeedBaseUrl = "https://raw.githubusercontent.com/halrad-com/mbxhub/main/";

    /// <summary><b>SHA-256</b> cert hash(es) of the code-signing cert MBXDist trusts.
    /// HALRAD LLC (CN=HALRAD LLC, O=HALRAD LLC, S=Washington, C=US), issued by
    /// Sectigo Public Code Signing CA R36, hardware token, valid to 2028-02-04.
    /// Multiple entries supported for rotation — add the successor cert alongside before the cutover.
    ///
    /// <para>These are SHA-256, not SHA-1. <c>signtool verify /v</c> prints a "SHA1 hash" line; that value
    /// will NOT match here. Read the right one with
    /// <c>cert.GetCertHashString(HashAlgorithmName.SHA256)</c>.</para></summary>
    public static readonly string[] PinnedThumbprints = { "C934BA3E5CF720F591E93591A04FE727A303D64EF2F60E1D21A24114744C266E" };

    /// <summary>Resource-name tokens for the embedded feed loader.</summary>
    public const string CatalogResourceSuffix = "catalog.json";
    public const string ManifestResourcePrefix = "manifests.";

    /// <summary>Reserved sink configuration. The CLI writes local diagnostics and does not send telemetry.</summary>
    public const string? TelemetrySinkUrl = null;
}
