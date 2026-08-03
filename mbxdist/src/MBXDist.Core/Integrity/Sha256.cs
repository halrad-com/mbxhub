using System.Security.Cryptography;

namespace MBXDist.Core.Integrity;

/// <summary>File SHA-256 as lowercase hex (integrity cross-check under the Authenticode signature).</summary>
public static class Sha256
{
    public static string OfFile(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    public static bool Verify(string path, string expectedHex)
        => string.Equals(OfFile(path), expectedHex, StringComparison.OrdinalIgnoreCase);
}
