namespace MBXDist.Core.Verify;

/// <summary>Accepts a signature only if it is trusted AND the signer thumbprint matches one pinned in the client.
/// Thumbprints are normalized (hex digits only, uppercased) so formatting differences do not matter.</summary>
public sealed class PinnedThumbprintPolicy
{
    private readonly HashSet<string> _pinned;

    public PinnedThumbprintPolicy(IEnumerable<string> pinnedThumbprints)
        => _pinned = new HashSet<string>(pinnedThumbprints.Select(Normalize), StringComparer.Ordinal);

    public bool IsAcceptable(SignatureResult result)
    {
        if (!result.Trusted || string.IsNullOrEmpty(result.Thumbprint)) return false;
        return _pinned.Contains(Normalize(result.Thumbprint));
    }

    private static string Normalize(string t)
        => new string(t.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant();
}
