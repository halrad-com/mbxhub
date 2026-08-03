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
}
