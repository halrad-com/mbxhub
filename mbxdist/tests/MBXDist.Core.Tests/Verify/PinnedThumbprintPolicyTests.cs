using MBXDist.Core.Verify;

namespace MBXDist.Core.Tests.Verify;

public class PinnedThumbprintPolicyTests
{
    private static PinnedThumbprintPolicy Policy() =>
        new(new[] { "AA BB CC dd ee" }); // spaces + mixed case on purpose

    [Fact]
    public void Accepts_trusted_with_matching_thumbprint_ignoring_format()
    {
        Assert.True(Policy().IsAcceptable(new SignatureResult(true, "aabbccddee")));
        Assert.True(Policy().IsAcceptable(new SignatureResult(true, "AA:BB:CC:DD:EE")));
    }

    [Fact]
    public void Rejects_untrusted_even_if_thumbprint_matches()
    {
        Assert.False(Policy().IsAcceptable(new SignatureResult(false, "aabbccddee")));
    }

    [Fact]
    public void Rejects_trusted_with_non_pinned_thumbprint()
    {
        Assert.False(Policy().IsAcceptable(new SignatureResult(true, "ffffffffff")));
    }

    [Fact]
    public void Rejects_when_thumbprint_null_or_empty()
    {
        Assert.False(Policy().IsAcceptable(new SignatureResult(true, null)));
        Assert.False(Policy().IsAcceptable(new SignatureResult(true, "")));
    }

    /// <summary>The pinned value is a SHA-256 cert hash, so a SHA-1 thumbprint of the same certificate
    /// must not satisfy it. Both values below are the real HALRAD LLC signing cert, read from
    /// publish/MBXHub.exe — this pins the intent that signtool's "SHA1 hash" line is the wrong one to
    /// paste into AppInfo.PinnedThumbprints.</summary>
    [Fact]
    public void Sha1_thumbprint_of_the_same_cert_does_not_satisfy_a_sha256_pin()
    {
        const string sha256 = "C934BA3E5CF720F591E93591A04FE727A303D64EF2F60E1D21A24114744C266E";
        const string sha1 = "7267AEC2ABA9C2F85BEE3D3AC9544417B6694FB4";

        var policy = new PinnedThumbprintPolicy(new[] { sha256 });

        Assert.True(policy.IsAcceptable(new SignatureResult(true, sha256)));
        Assert.False(policy.IsAcceptable(new SignatureResult(true, sha1)));
    }
}
