using MBXDist.Core.Apply;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Verify;
namespace MBXDist.Core.Orchestration;

public sealed record ResourceOutcome(string Filename, string TargetPath, ApplyOutcome? Applied, string? Error, bool VerificationFailed = false);
public sealed record PackageUpdateResult(string Id, string Version, IReadOnlyList<ResourceOutcome> Resources)
{
    public bool AnyPending => Resources.Any(r => r.Applied == ApplyOutcome.StagedPending);
    public bool AnyFailed => Resources.Any(r => r.Error is not null);
    public bool AllApplied => Resources.Count > 0 && Resources.All(r => r.Applied == ApplyOutcome.Applied);
}

/// <summary>Prepares and verifies the whole package, then applies recoverably. State is never a trust root.</summary>
public sealed class UpdateService
{
    private readonly IFeedClient _feed;
    private readonly ISignatureCheck _sig;
    private readonly PinnedThumbprintPolicy _policy;
    private readonly Updater _updater;
    private readonly string _stagingDir;
    private readonly IReadOnlyDictionary<string, string> _targetRoots;
    private readonly Action<string, string>? _log;
    public UpdateService(IFeedClient feed, ISignatureCheck sig, PinnedThumbprintPolicy policy, Updater updater, string stagingDir,
        IReadOnlyDictionary<string, string> targetRoots, Action<string, string>? log = null)
    { _feed = feed; _sig = sig; _policy = policy; _updater = updater; _stagingDir = stagingDir; _targetRoots = targetRoots; _log = log; }

    public string ResolveTarget(ResourceEntry resource)
    {
        if (!_targetRoots.TryGetValue(resource.Target.Root, out var root) || string.IsNullOrWhiteSpace(root))
            throw new InvalidDataException($"no install target configured for root '{resource.Target.Root}'");
        return FeedValidation.UnderRoot(root, resource.Target.Path);
    }

    public string? VerifyFile(ResourceEntry resource, string path)
    {
        try
        {
            if (!File.Exists(path)) return "file missing";
            if (new FileInfo(path).Length != resource.Size) return "size mismatch";
            if (!Sha256.Verify(path, resource.Sha256)) return "sha256 mismatch";
            if (resource.Authenticode && !_policy.IsAcceptable(_sig.Check(path))) return "signature not trusted or thumbprint not pinned";
            return null;
        }
        catch (IOException ex) { return $"file cannot be verified: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { return $"file cannot be verified: {ex.Message}"; }
    }

    public async Task<PackageUpdateResult> ApplyPackageAsync(Manifest manifest, LocalState state, string nowIso,
        CancellationToken ct = default, Action? persist = null)
    {
        var outcomes = new List<ResourceOutcome>();
        var ready = new List<(ResourceEntry Resource, string Target, string Source)>();
        try { FeedValidation.Manifest(manifest); }
        catch (Exception ex) when (ex is InvalidDataException or FormatException)
        { return new(manifest.Id, manifest.Version, new[] { new ResourceOutcome("manifest", "", null, ex.Message, true) }); }

        // Resolve every target before any download. Detect aliases through different target roots too.
        var paths = new Dictionary<ResourceEntry, string>();
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in manifest.Resources)
        {
            try
            {
                var target = ResolveTarget(resource);
                FeedValidation.UnderRoot(_targetRoots[resource.Target.Root], resource.Target.Path + ".pending");
                if (!distinct.Add(target) || !distinct.Add(target + ".pending")) throw new InvalidDataException("duplicate resolved target or pending path");
                paths.Add(resource, target);
            }
            catch (Exception ex) { outcomes.Add(new(resource.Filename, "", null, ex.Message)); }
        }
        if (outcomes.Count > 0) return new(manifest.Id, manifest.Version, outcomes);

        foreach (var resource in manifest.Resources)
        {
            ct.ThrowIfCancellationRequested();
            var target = paths[resource];
            try
            {
                if (VerifyFile(resource, target) is null)
                { ready.Add((resource, target, target)); continue; }
                var source = target + ".pending";
                if (!File.Exists(source) || VerifyFile(resource, source) is not null)
                {
                    source = FeedValidation.UnderRoot(_stagingDir, manifest.Id + "/" + manifest.Version + "/" + resource.Filename);
                    if (VerifyFile(resource, source) is not null)
                    {
                        _log?.Invoke("Debug", $"download {manifest.Id} {resource.Url}");
                        await _feed.DownloadToAsync(resource.Url, source, ct);
                    }
                    else _log?.Invoke("Debug", $"reuse verified staging for {manifest.Id}/{resource.Filename}");
                }
                var error = VerifyFile(resource, source);
                if (error is not null)
                {
                    _log?.Invoke("Warning", $"verification refused {manifest.Id}/{resource.Filename}: {error}");
                    outcomes.Add(new(resource.Filename, target, null, error, true));
                }
                else
                { _log?.Invoke("Debug", $"verified {manifest.Id}/{resource.Filename}"); ready.Add((resource, target, source)); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { outcomes.Add(new(resource.Filename, target, null, ex.Message)); }
        }
        if (outcomes.Count > 0) return new(manifest.Id, manifest.Version, outcomes);

        state.InProgress[manifest.Id] = manifest.Version;
        persist?.Invoke(); // journal intent before the first target changes
        foreach (var item in ready)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Recheck after preparation; do not apply changed pending/staging bytes.
                ResolveTarget(item.Resource);
                var error = VerifyFile(item.Resource, item.Source);
                if (error is not null) { outcomes.Add(new(item.Resource.Filename, item.Target, null, error, true)); break; }
                var applied = ApplyOutcome.Applied;
                if (item.Source != item.Target) applied = _updater.ApplyStaged(item.Source, item.Target).Outcome;
                else if (File.Exists(item.Target + ".pending")) File.Delete(item.Target + ".pending");
                outcomes.Add(new(item.Resource.Filename, item.Target, applied, null));
                _log?.Invoke("Information", $"{manifest.Id}/{item.Resource.Filename}: {applied}");
            }
            catch (Exception ex) { outcomes.Add(new(item.Resource.Filename, item.Target, null, ex.Message)); break; }
        }
        var result = new PackageUpdateResult(manifest.Id, manifest.Version, outcomes);
        if (result.AllApplied && outcomes.Count == manifest.Resources.Count)
        {
            state.Packages[manifest.Id] = new PackageState
            {
                Version = manifest.Version,
                InstalledAt = nowIso,
                Resources = ready.Select(r => new ResourceState { Filename = r.Resource.Filename, Target = r.Target, Sha256 = r.Resource.Sha256 }).ToList()
            };
            state.InProgress.Remove(manifest.Id);
        }
        persist?.Invoke();
        return result;
    }
}
