using MBXDist.Platform.Windows;

namespace MBXDist.Platform.Windows.Tests;

public class WindowsAuthenticodeCheckTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-auth-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Unsigned_file_is_not_trusted_and_has_no_thumbprint()
    {
        Directory.CreateDirectory(_dir);
        var f = Path.Combine(_dir, "unsigned.bin");
        File.WriteAllBytes(f, new byte[] { 1, 2, 3, 4 });

        var result = new WindowsAuthenticodeCheck().Check(f);

        Assert.False(result.Trusted);
        Assert.Null(result.Thumbprint);
    }

    [Fact]
    public void Signed_fixture_is_trusted_with_expected_thumbprint()
    {
        // Guarded: runs only when a real signed binary is provided out-of-band.
        var fixture = Environment.GetEnvironmentVariable("MBXDIST_SIGNED_FIXTURE");
        var expected = Environment.GetEnvironmentVariable("MBXDIST_SIGNED_THUMBPRINT");
        if (string.IsNullOrEmpty(fixture) || string.IsNullOrEmpty(expected))
            return; // skipped without a fixture — see manual checklist in the plan

        var result = new WindowsAuthenticodeCheck().Check(fixture);

        Assert.True(result.Trusted);
        Assert.Equal(
            new string(expected.Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant(),
            new string((result.Thumbprint ?? "").Where(char.IsAsciiHexDigit).ToArray()).ToUpperInvariant());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
