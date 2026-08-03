using MBXDist.Core.Net;
using MBXDist.Core.Verify;
using MBXDist.Core.Versioning;

namespace MBXDist.Core.Orchestration;

public enum SelfUpdateStatus
{
    UpToDate,       // marker version <= running version
    Staged,         // newer MBXDist downloaded + verified, ready to swap
    VerifyFailed,   // newer MBXDist downloaded but signature/thumbprint rejected — fail closed
    CheckFailed     // marker unreachable/unreadable — proceed with the running version
}

public sealed record SelfUpdateResult(SelfUpdateStatus Status, string? LatestVersion, string? StagedPath, string? Error);

/// <summary>Self-update check-and-stage: read the well-known version marker, and if it names a newer
/// version, download that signed MBXDist and verify it (Authenticode + pinned thumbprint) before
/// reporting it staged. The actual swap + re-exec is the caller's job (process-level, not testable here).
/// A missing/unreachable marker is CheckFailed, not fatal — the running version proceeds.</summary>
public sealed class SelfUpdater
{
    public const string DefaultMarkerUrl = "mbxdist/latest.txt";

    private readonly IFeedClient _feed;
    private readonly ISignatureCheck _sig;
    private readonly PinnedThumbprintPolicy _policy;
    private readonly string _markerUrl;

    public SelfUpdater(IFeedClient feed, ISignatureCheck sig, PinnedThumbprintPolicy policy, string markerUrl = DefaultMarkerUrl)
    {
        _feed = feed;
        _sig = sig;
        _policy = policy;
        _markerUrl = markerUrl;
    }

    public async Task<SelfUpdateResult> CheckAndStageAsync(string currentVersion, string stagingDir, CancellationToken ct = default)
    {
        string latest;
        try
        {
            latest = await _feed.GetTextAsync(_markerUrl, ct);
        }
        catch (Exception ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.CheckFailed, null, null, ex.Message);
        }

        try
        {
            if (PackageVersion.Parse(latest).CompareTo(PackageVersion.Parse(currentVersion)) <= 0)
                return new SelfUpdateResult(SelfUpdateStatus.UpToDate, latest, null, null);
        }
        catch (FormatException ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.CheckFailed, latest, null, $"bad marker version: {ex.Message}");
        }

        var staged = Path.Combine(stagingDir, "mbxdist-" + latest + ".exe");
        try
        {
            await _feed.DownloadToAsync($"mbxdist/{latest}/mbxdist.exe", staged, ct);
        }
        catch (Exception ex)
        {
            return new SelfUpdateResult(SelfUpdateStatus.CheckFailed, latest, null, ex.Message);
        }

        if (!_policy.IsAcceptable(_sig.Check(staged)))
        {
            try { File.Delete(staged); } catch { /* best effort */ }
            return new SelfUpdateResult(SelfUpdateStatus.VerifyFailed, latest, null, "downloaded mbxdist failed signature/thumbprint verification");
        }

        return new SelfUpdateResult(SelfUpdateStatus.Staged, latest, staged, null);
    }
}
