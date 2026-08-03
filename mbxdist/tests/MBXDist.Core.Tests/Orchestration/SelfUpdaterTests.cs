using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.Verify;

namespace MBXDist.Core.Tests.Orchestration;

public class SelfUpdaterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-self-" + Guid.NewGuid().ToString("N"));
    private const string Thumb = "AABBCCDDEE";

    private sealed class FakeFeed : IFeedClient
    {
        public string? Marker { get; set; }
        public bool ThrowOnMarker { get; set; }
        public bool ThrowOnDownload { get; set; }
        public Task<string> GetTextAsync(string relativeUrl, CancellationToken ct = default)
            => ThrowOnMarker ? throw new HttpRequestException("offline") : Task.FromResult(Marker ?? "");
        public async Task DownloadToAsync(string relativeUrl, string destPath, CancellationToken ct = default)
        {
            if (ThrowOnDownload) throw new HttpRequestException("download failed");
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await File.WriteAllTextAsync(destPath, "NEWEXE", ct);
        }
    }

    private sealed class FakeSig : ISignatureCheck
    {
        public SignatureResult Result { get; set; } = new(true, Thumb);
        public SignatureResult Check(string filePath) => Result;
    }

    private SelfUpdater Make(FakeFeed feed, FakeSig? sig = null)
        => new(feed, sig ?? new FakeSig(), new PinnedThumbprintPolicy(new[] { Thumb }));

    [Fact]
    public async Task Marker_equal_or_older_is_up_to_date()
    {
        var r = await Make(new FakeFeed { Marker = "0.1.0" }).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.UpToDate, r.Status);

        var r2 = await Make(new FakeFeed { Marker = "0.0.9" }).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.UpToDate, r2.Status);
    }

    [Fact]
    public async Task Newer_marker_downloads_verifies_and_stages()
    {
        var r = await Make(new FakeFeed { Marker = "0.2.0" }).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.Staged, r.Status);
        Assert.Equal("0.2.0", r.LatestVersion);
        Assert.True(File.Exists(r.StagedPath));
    }

    [Fact]
    public async Task Newer_marker_with_bad_signature_fails_closed_and_deletes_staged()
    {
        var sig = new FakeSig { Result = new SignatureResult(false, Thumb) };
        var r = await Make(new FakeFeed { Marker = "0.2.0" }, sig).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.VerifyFailed, r.Status);
        Assert.Null(r.StagedPath);
        Assert.Empty(Directory.Exists(_dir) ? Directory.GetFiles(_dir) : Array.Empty<string>());
    }

    [Fact]
    public async Task Unreachable_marker_is_check_failed_not_fatal()
    {
        var r = await Make(new FakeFeed { ThrowOnMarker = true }).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.CheckFailed, r.Status);
    }

    [Fact]
    public async Task Garbage_marker_is_check_failed()
    {
        var r = await Make(new FakeFeed { Marker = "not-a-version" }).CheckAndStageAsync("0.1.0", _dir);
        Assert.Equal(SelfUpdateStatus.CheckFailed, r.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
