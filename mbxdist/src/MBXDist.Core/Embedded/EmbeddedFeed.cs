using System.Reflection;
using System.Text.Json;
using MBXDist.Core.Model;

namespace MBXDist.Core.Embedded;

/// <summary>Loads the catalog + per-package manifests baked into an assembly as embedded resources.
/// Resources are matched by name suffix so the exact resource namespace does not matter.</summary>
public sealed class EmbeddedFeed
{
    private readonly Assembly _asm;
    private readonly string _catalogSuffix;
    private readonly string _manifestPrefixToken;

    public EmbeddedFeed(Assembly asm, string catalogResourceSuffix, string manifestResourcePrefix)
    {
        _asm = asm;
        _catalogSuffix = catalogResourceSuffix;
        _manifestPrefixToken = manifestResourcePrefix;
    }

    public Catalog LoadCatalog()
    {
        using var s = OpenBySuffix(_catalogSuffix);
        return JsonSerializer.Deserialize<Catalog>(s, FeedJson.Options)
               ?? throw new InvalidDataException("catalog resource deserialized to null");
    }

    public Manifest LoadManifest(string packageId)
    {
        // e.g. resource "...Fixtures.manifests.halrad.mbxhub.core.json"
        var suffix = _manifestPrefixToken + packageId + ".json";
        using var s = OpenBySuffix(suffix);
        return JsonSerializer.Deserialize<Manifest>(s, FeedJson.Options)
               ?? throw new InvalidDataException($"manifest resource for '{packageId}' deserialized to null");
    }

    private Stream OpenBySuffix(string suffix)
    {
        var name = _asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"no embedded resource ending with '{suffix}'");
        return _asm.GetManifestResourceStream(name)
               ?? throw new FileNotFoundException($"embedded resource stream null for '{name}'");
    }
}
