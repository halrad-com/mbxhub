using System.Text.Json;
using MBXDist.Core.Apply;
using MBXDist.Core.Diff;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.State;
using MBXDist.Core.Verify;
using MBXDist.Core.Versioning;
namespace MBXDist.App;

public sealed class VerificationException(string message) : Exception(message);
public sealed record Inspection(string Id, string? InstalledVersion, string RecommendedVersion, string Status, string? Detail = null);

/// <summary>One command invocation. Embedded metadata and signature policy remain the trust authority.</summary>
public sealed class CliRunner
{
    private readonly Catalog _catalog;
    private readonly Func<string, Manifest> _manifest;
    private readonly ISignatureCheck _signature;
    private readonly PinnedThumbprintPolicy _policy;
    private readonly Func<string, IFeedClient> _client;
    public CliRunner(Catalog catalog, Func<string, Manifest> manifest, ISignatureCheck signature, PinnedThumbprintPolicy policy,
        Func<string, IFeedClient>? client = null)
    {
        _catalog = catalog;
        _manifest = id =>
        {
            var result = manifest(id);
            var entry = catalog.Packages.Single(p => p.Id == id);
            if (result.Id != entry.Id || result.Version != entry.Version) throw new InvalidDataException("catalog/manifest identity or version mismatch");
            return result;
        };
        _signature = signature; _policy = policy; _client = client ?? (url => new HttpFeedClient(url));
    }

    public async Task<int> RunAsync(CliOptions options, string[] originalArgs)
    {
        var store = new StateStore(options.Config);
        var directory = Path.GetDirectoryName(options.Config)!;
        // Every config owns its staging and diagnostics, even when two configs share a directory.
        var working = options.Config + ".work";
        void Log(string level, string message)
        {
            if (options.DryRun) return;
            try
            {
                Directory.CreateDirectory(working);
                var path = Path.Combine(working, "operations.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { timestamp = DateTime.UtcNow, level, message }) + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        int Output(int code, object value, string? text = null)
        {
            Console.WriteLine(options.Json ? JsonSerializer.Serialize(value, FeedJson.Options) : text ?? JsonSerializer.Serialize(value, FeedJson.Options));
            return code;
        }
        foreach (var id in options.Verb is "init" ? Enumerable.Empty<string>() : options.Words)
            if (!_catalog.Packages.Any(p => p.Id == id)) throw new ArgumentException($"unknown package '{id}'");

        switch (options.Verb)
        {
            case "version": return Output(0, new { version = AppInfo.Version }, $"MBXDist {AppInfo.Version}");
            case "help":
                return Output(0, new { commands = new[] { "check", "update [id...]", "verify [id...]", "repair [id...]", "status", "list", "init <root> <absolute-path>", "version", "validate-feed --feed-root <path>" } },
                "MBXDist: check | update [id...] | verify [id...] | repair [id...] | status | list | init <root> <absolute-path> | version\nOptions: --config <state-file> --feed <url> --json --yes --no-self-update --dry-run\nUpdate without IDs updates installed packages only; select an ID to install it. Dry-run never changes files.");
            case "list": return Output(0, _catalog, string.Join(Environment.NewLine, _catalog.Packages.Select(p => $"{p.Id}  {p.Version}")));
            case "validate-feed":
                try
                {
                    FeedValidation.Catalog(_catalog, _manifest);
                    var feedClient = new HttpFeedClient(options.Feed);
                    var verifier = new UpdateService(feedClient, _signature, _policy, new Updater(), working, new Dictionary<string, string>());
                    foreach (var package in _catalog.Packages)
                        foreach (var resource in _manifest(package.Id).Resources)
                        {
                            var error = verifier.VerifyFile(resource, FeedValidation.UnderRoot(options.FeedRoot!, resource.Url));
                            if (error is not null) throw new InvalidDataException($"{package.Id}/{resource.Filename}: {error}");
                        }
                    return Output(0, new { valid = true }, "Feed verified.");
                }
                catch (Exception ex) { throw new VerificationException(ex.Message); }
        }

        // Dry-run bypasses all locks, identity writes, diagnostics and self-update.
        if (!options.DryRun && !options.NoSelfUpdate && options.Verb is "check" or "update" or "ux")
        {
            if (IsStandalone()) // only the published carrier can replace itself
            {
                using var ownLock = new FileStream(Environment.ProcessPath + ".update-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                SelfSwap.Cleanup(Environment.ProcessPath!);
                var transport = _client(options.Feed);
                try
                {
                    var result = await new SelfUpdater(transport, _signature, _policy).CheckAndStageAsync(AppInfo.Version, Path.Combine(working, "self"));
                    Log("Debug", $"self-update: {result.Status}; {result.Error}");
                    if (result.Status == SelfUpdateStatus.VerifyFailed) throw new VerificationException(result.Error!);
                    if (result.Status == SelfUpdateStatus.Staged)
                    {
                        Console.Error.WriteLine($"Updating MBXDist to {result.LatestVersion}...");
                        return await SelfSwap.RunAsync(result.StagedPath!, Environment.ProcessPath!, originalArgs.Concat(new[] { "--no-self-update" }));
                    }
                }
                finally { (transport as IDisposable)?.Dispose(); }
            }
        }

        using var stateLock = options.DryRun ? null : store.AcquireLock();
        var state = store.Load();
        if (!options.DryRun)
        { StateStore.EnsureClientId(state); store.Save(state); Log("Information", $"{options.Verb} client={state.ClientId}"); }
        var network = _client(options.Feed);
        try
        {
            var service = new UpdateService(network, _signature, _policy, new Updater(), Path.Combine(working, "staging"), state.TargetRoots, Log);
            switch (options.Verb)
            {
                case "status": return Output(0, new { state.Packages, state.InProgress, state.TargetRoots });
                case "init":
                    if (options.Words.Count == 2)
                    {
                        FeedValidation.Name(options.Words[0]);
                        if (!Path.IsPathFullyQualified(options.Words[1])) throw new ArgumentException("install path must be absolute");
                        state.TargetRoots[options.Words[0]] = Path.GetFullPath(options.Words[1]); store.Save(state);
                    }
                    return Output(0, state.TargetRoots);
                case "check":
                case "ux":
                    var inspections = Inspect(state, service);
                    bool actionable = inspections.Any(p => p.Status is "updateAvailable" or "corrupt" or "pending");
                    if (options.Verb == "check") return Output(actionable ? 10 : 0, new { packages = inspections }, string.Join(Environment.NewLine, inspections.Select(p => $"{p.Id}: {p.Status}{(p.Detail is null ? "" : " - " + p.Detail)}")));
                    foreach (var p in inspections) Console.WriteLine($"{p.Id}: {p.Status}");
                    if (!actionable) return Output(0, new { packages = Array.Empty<object>() }, "No installed packages need updates. Use list, init, and update <id> to install a package.");
                    Console.Write("Apply these updates? [y/N] ");
                    if (!options.Yes && !string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase)) return 10;
                    break;
                case "verify":
                    var checks = VerifySelected(options.Words, state, service);
                    int verifyCode = checks.Any(p => p.Status != "verified") ? 30 : 0;
                    return Output(verifyCode, new { packages = checks }, string.Join(Environment.NewLine, checks.Select(p => $"{p.Id}: {p.Status} {p.Detail}")));
            }

            bool repair = options.Verb == "repair";
            var selected = options.Words.Count > 0 ? options.Words.Distinct().ToList()
                : _catalog.Packages.Where(p => state.Packages.ContainsKey(p.Id) || state.InProgress.ContainsKey(p.Id)).Select(p => p.Id).ToList();
            // Walk the full trusted graph before pruning packages whose own files are healthy.
            var roots = new List<string>();
            foreach (var id in selected)
            {
                var manifest = _manifest(id);
                if (state.InProgress.TryGetValue(id, out var pendingVersion) && pendingVersion != manifest.Version)
                    throw new InvalidDataException($"{id}: interrupted version {pendingVersion} is not in this client's trusted metadata");
                if (state.Packages.TryGetValue(id, out var installed))
                {
                    int comparison = PackageVersion.Parse(installed.Version).CompareTo(PackageVersion.Parse(manifest.Version));
                    if (repair && comparison != 0) throw new VerificationException($"{id}: trusted metadata for installed version {installed.Version} is unavailable; use update for a newer recommendation");
                    if (comparison > 0) continue;
                }
                else if (repair) throw new InvalidDataException($"{id} is not recorded as installed; use update <id>");
                roots.Add(id);
            }
            var graph = new DependencyResolver(_manifest).ResolveClosure(roots, new LocalState());
            var order = new List<string>();
            foreach (var id in graph)
            {
                var manifest = _manifest(id);
                if (state.InProgress.TryGetValue(id, out var pendingVersion) && pendingVersion != manifest.Version)
                    throw new InvalidDataException($"{id}: interrupted version {pendingVersion} is not in this client's trusted metadata");
                if (state.Packages.TryGetValue(id, out var installed))
                {
                    int comparison = PackageVersion.Parse(installed.Version).CompareTo(PackageVersion.Parse(manifest.Version));
                    if (comparison > 0 || (repair && comparison != 0))
                        throw new VerificationException($"{id}: trusted metadata for installed dependency version {installed.Version} is unavailable");
                }
                try { FeedValidation.Manifest(manifest); }
                catch (Exception ex) { throw new VerificationException($"{id}: {ex.Message}"); }
                if (!state.Packages.TryGetValue(id, out installed) || installed.Version != manifest.Version
                    || state.InProgress.ContainsKey(id) || CheckManifest(manifest, service) is not null) order.Add(id);
            }
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Reserve(string owner, string target)
            {
                foreach (var path in new[] { Path.GetFullPath(target), Path.GetFullPath(target) + ".pending" })
                {
                    if (owners.TryGetValue(path, out var existing) && existing != owner)
                        throw new InvalidDataException($"packages '{owner}' and '{existing}' have overlapping install targets");
                    owners[path] = owner;
                }
            }
            // Recorded paths reserve ownership only; they never authorize installation or verification.
            foreach (var installed in state.Packages)
                foreach (var resource in installed.Value.Resources) Reserve(installed.Key, resource.Target);
            foreach (var id in state.InProgress.Keys.Union(graph))
            {
                var manifest = _manifest(id);
                if (state.InProgress.TryGetValue(id, out var pendingVersion) && pendingVersion != manifest.Version)
                    throw new InvalidDataException($"{id}: interrupted version {pendingVersion} is not in this client's trusted metadata");
                var packageTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var resource in manifest.Resources)
                {
                    var target = service.ResolveTarget(resource);
                    if (!packageTargets.Add(target) || !packageTargets.Add(target + ".pending"))
                        throw new InvalidDataException($"{id}: resources overlap target or pending paths");
                    Reserve(id, target);
                }
            }
            if (options.DryRun) return Output(0, new { dryRun = true, packages = order }, order.Count == 0 ? "No changes." : "Would apply: " + string.Join(", ", order));
            var results = new List<PackageUpdateResult>();
            var blocked = new HashSet<string>();
            foreach (var id in graph)
            {
                var manifest = _manifest(id);
                var failedDependency = manifest.Dependencies.FirstOrDefault(dep => blocked.Contains(dep.Id) || state.InProgress.ContainsKey(dep.Id)
                    || !state.Packages.TryGetValue(dep.Id, out var package) || !PackageVersion.Parse(package.Version).Satisfies(PackageVersion.Parse(dep.MinVersion)));
                if (failedDependency is not null)
                {
                    blocked.Add(id); results.Add(new(id, manifest.Version, new[] { new ResourceOutcome("dependency", "", null, $"required package '{failedDependency.Id}' is not available") })); continue;
                }
                if (!order.Contains(id)) continue;
                var result = await service.ApplyPackageAsync(manifest, state, DateTime.UtcNow.ToString("o"), persist: () => store.Save(state));
                results.Add(result); if (!result.AllApplied) blocked.Add(id);
            }
            int exit = results.Any(p => p.Resources.Any(r => r.VerificationFailed)) ? 30 : results.Any(p => p.AnyFailed) ? 1 : results.Any(p => p.AnyPending) ? 20 : 0;
            Log(exit == 0 ? "Information" : "Warning", $"{options.Verb} exit={exit}");
            return Output(exit, new { packages = results }, results.Count == 0 ? "No changes." : string.Join(Environment.NewLine, results.SelectMany(p => p.Resources.Select(r => $"{p.Id}/{r.Filename}: {r.Error ?? r.Applied?.ToString()}"))));
        }
        catch (Exception ex) { Log("Error", ex.Message); throw; }
        finally { (network as IDisposable)?.Dispose(); }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "An empty Location deliberately identifies bundled assemblies; it is never used as a filesystem path.")]
    private static bool IsStandalone() => string.IsNullOrEmpty(typeof(AppInfo).Assembly.Location);

    private string? CheckManifest(Manifest manifest, UpdateService service)
    {
        try
        {
            FeedValidation.Manifest(manifest);
            foreach (var resource in manifest.Resources)
            { var error = service.VerifyFile(resource, service.ResolveTarget(resource)); if (error is not null) return $"{resource.Filename}: {error}"; }
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }
    private List<Inspection> Inspect(LocalState state, UpdateService service) => _catalog.Packages.Select(entry =>
    {
        state.Packages.TryGetValue(entry.Id, out var installed);
        string status = installed is null ? "missing" : PackageVersion.Parse(installed.Version).CompareTo(PackageVersion.Parse(entry.Version)) switch
        { < 0 => "updateAvailable", > 0 => "ahead", _ => "upToDate" };
        string? detail = null;
        if (state.InProgress.ContainsKey(entry.Id)) status = "pending";
        else if (status == "upToDate" && (detail = CheckManifest(_manifest(entry.Id), service)) is not null) status = "corrupt";
        return new Inspection(entry.Id, installed?.Version, entry.Version, status, detail);
    }).ToList();
    private List<Inspection> VerifySelected(List<string> selected, LocalState state, UpdateService service)
    {
        var ids = selected.Count > 0 ? selected : state.Packages.Keys.ToList();
        return ids.Select(id =>
        {
            if (!_catalog.Packages.Any(p => p.Id == id)) return new Inspection(id, state.Packages[id].Version, "", "unavailable", "package not in this client's catalog");
            var manifest = _manifest(id);
            if (!state.Packages.TryGetValue(id, out var installed)) return new Inspection(id, null, manifest.Version, "missing");
            if (installed.Version != manifest.Version) return new Inspection(id, installed.Version, manifest.Version, "unavailable", "trusted metadata for installed version unavailable");
            var error = CheckManifest(manifest, service);
            if (state.InProgress.ContainsKey(id)) error = "package has an interrupted or pending update";
            return new Inspection(id, installed.Version, manifest.Version, error is null ? "verified" : "corrupt", error);
        }).ToList();
    }
}
