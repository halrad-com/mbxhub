using MBXDist.Core.Apply;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Verify;

namespace MBXDist.Core.Orchestration;

public sealed record ResourceOutcome(string Filename, string TargetPath, ApplyOutcome? Applied, string? Error);

public sealed record PackageUpdateResult(string Id, string Version, IReadOnlyList<ResourceOutcome> Resources)
{
    public bool AnyPending => Resources.Any(r => r.Applied == ApplyOutcome.StagedPending);
    public bool AnyFailed => Resources.Any(r => r.Error is not null);
    public bool AllApplied => Resources.Count > 0 && Resources.All(r => r.Applied == ApplyOutcome.Applied);
}

/// <summary>Applies one package version: for each resource, download to staging, verify SHA-256 and the
/// Authenticode signature (trusted + pinned thumbprint), then apply lock-aware. Records local state only
/// when every resource applied cleanly; a partial/pending/failed apply leaves state for the next run.</summary>
public sealed class UpdateService
{
    private readonly IFeedClient _feed;
    private readonly ISignatureCheck _sig;
    private readonly PinnedThumbprintPolicy _policy;
    private readonly Updater _updater;
    private readonly string _stagingDir;
    private readonly IReadOnlyDictionary<string, string> _targetRoots;

    public UpdateService(
        IFeedClient feed,
        ISignatureCheck sig,
        PinnedThumbprintPolicy policy,
        Updater updater,
        string stagingDir,
        IReadOnlyDictionary<string, string> targetRoots)
    {
        _feed = feed;
        _sig = sig;
        _policy = policy;
        _updater = updater;
        _stagingDir = stagingDir;
        _targetRoots = targetRoots;
    }

    public async Task<PackageUpdateResult> ApplyPackageAsync(Manifest manifest, LocalState state, string nowIso, CancellationToken ct = default)
    {
        var outcomes = new List<ResourceOutcome>();
        var recorded = new List<ResourceState>();

        foreach (var res in manifest.Resources)
        {
            var targetPath = ResolveTarget(res);
            if (targetPath is null)
            {
                outcomes.Add(new ResourceOutcome(res.Filename, "", null, $"no install target configured for root '{res.Target.Root}'"));
                continue;
            }

            var staged = Path.Combine(_stagingDir, Sanitize(manifest.Id), res.Filename);
            try
            {
                await _feed.DownloadToAsync(res.Url, staged, ct);

                if (!Sha256.Verify(staged, res.Sha256))
                {
                    outcomes.Add(new ResourceOutcome(res.Filename, targetPath, null, "sha256 mismatch"));
                    continue;
                }

                // Third-party binaries we don't sign (authenticode: false) are gated by SHA-256 alone —
                // trustworthy because the manifest carrying that hash ships inside the signed client.
                if (res.Authenticode && !_policy.IsAcceptable(_sig.Check(staged)))
                {
                    outcomes.Add(new ResourceOutcome(res.Filename, targetPath, null, "signature not trusted or thumbprint not pinned"));
                    continue;
                }

                var apply = _updater.ApplyStaged(staged, targetPath);
                outcomes.Add(new ResourceOutcome(res.Filename, targetPath, apply.Outcome, null));
                recorded.Add(new ResourceState { Filename = res.Filename, Target = targetPath, Sha256 = res.Sha256 });
            }
            catch (Exception ex)
            {
                outcomes.Add(new ResourceOutcome(res.Filename, targetPath, null, ex.Message));
            }
        }

        var result = new PackageUpdateResult(manifest.Id, manifest.Version, outcomes);
        if (result.AllApplied)
        {
            state.Packages[manifest.Id] = new PackageState
            {
                Version = manifest.Version,
                InstalledAt = nowIso,
                Resources = recorded
            };
        }
        return result;
    }

    private string? ResolveTarget(ResourceEntry res)
    {
        if (!_targetRoots.TryGetValue(res.Target.Root, out var root) || string.IsNullOrEmpty(root))
            return null;
        return Path.Combine(root, res.Target.Path);
    }

    private static string Sanitize(string id) => id.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
}
