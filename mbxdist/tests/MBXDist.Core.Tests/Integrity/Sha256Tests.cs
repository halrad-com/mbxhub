using MBXDist.Core.Integrity;

namespace MBXDist.Core.Tests.Integrity;

public class Sha256Tests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "mbxdist-sha-" + Guid.NewGuid().ToString("N") + ".bin");

    // SHA-256 of ASCII "abc"
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void OfFile_matches_known_vector()
    {
        File.WriteAllText(_file, "abc");
        Assert.Equal(AbcHash, Sha256.OfFile(_file));
    }

    [Fact]
    public void Verify_true_on_match_false_on_tamper()
    {
        File.WriteAllText(_file, "abc");
        Assert.True(Sha256.Verify(_file, AbcHash.ToUpperInvariant())); // case-insensitive
        File.WriteAllText(_file, "abd");
        Assert.False(Sha256.Verify(_file, AbcHash));
    }

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }
}
