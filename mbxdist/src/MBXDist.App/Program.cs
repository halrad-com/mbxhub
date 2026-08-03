using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MBXDist.App;
using MBXDist.Core.Apply;
using MBXDist.Core.Diff;
using MBXDist.Core.Embedded;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.State;
using MBXDist.Core.Telemetry;
using MBXDist.Core.Verify;
using MBXDist.Platform.Windows;

// mbxdist — dual-mode entry point.
//   Interactive UX : launched with no verb in a real console -> status table + prompt to apply.
//   Headless CLI   : verbs + flags + exit codes for ARIA / Task Scheduler / scripts.
// Exit codes: 0 ok/up-to-date · 10 update available (check) · 20 locked resource staged pending ·
//             30 signature/verification failure · 1 other error.

var flags = args.Where(a => a.StartsWith("--")).Select(a => a.ToLowerInvariant()).ToHashSet();
var words = args.Where(a => !a.StartsWith("--")).ToArray();

bool yes = flags.Contains("--yes");
bool json = flags.Contains("--json");
bool noSelfUpdate = flags.Contains("--no-self-update");

var feedBase = GetFlagValue(args, "--feed") ?? AppInfo.DefaultFeedBaseUrl;

var feed = new EmbeddedFeed(Assembly.GetExecutingAssembly(), AppInfo.CatalogResourceSuffix, AppInfo.ManifestResourcePrefix);
var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MBXDist");
var stateStore = new StateStore(Path.Combine(configDir, "mbxdist-state.json"));
var policy = new PinnedThumbprintPolicy(AppInfo.PinnedThumbprints);
var telemetry = new TelemetrySink(AppInfo.TelemetrySinkUrl, Path.Combine(configDir, "telemetry-queue.jsonl"));

CleanupOldSelfBinary();

bool interactive = words.Length == 0 && !Console.IsInputRedirected && !Console.IsOutputRedirected && !json;
string verb = words.Length > 0 ? words[0].ToLowerInvariant() : (interactive ? "ux" : "check");

try
{
    switch (verb)
    {
        case "version":
        case "--version":
            Console.WriteLine($"MBXDist {AppInfo.Version}");
            return 0;

        case "list":
            foreach (var p in feed.LoadCatalog().Packages)
                Console.WriteLine($"{p.Id}  {p.Version}");
            return 0;

        case "status":
        {
            var state = stateStore.Load();
            if (state.Packages.Count == 0) { Console.WriteLine("No packages recorded as installed."); return 0; }
            foreach (var (id, ps) in state.Packages)
                Console.WriteLine($"{id}  {ps.Version}");
            return 0;
        }

        case "init":
        {
            // init                 -> list configured target roots
            // init <root> <path>   -> map a target root to an absolute directory
            var state = stateStore.Load();
            StateStore.EnsureClientId(state);
            if (words.Length >= 3)
            {
                var root = words[1];
                var path = Path.GetFullPath(words[2]);
                state.TargetRoots[root] = path;
                stateStore.Save(state);
                Console.WriteLine($"target root '{root}' -> {path}");
                return 0;
            }
            if (state.TargetRoots.Count == 0)
            {
                Console.WriteLine("No target roots configured. Usage: mbxdist init <root> <path>");
                Console.WriteLine("Roots referenced by the catalog:");
                foreach (var id in feed.LoadCatalog().Packages.Select(p => p.Id))
                    foreach (var r in feed.LoadManifest(id).Resources.Select(r => r.Target.Root).Distinct())
                        Console.WriteLine($"  {r}  (needed by {id})");
                return 0;
            }
            foreach (var (root, path) in state.TargetRoots)
                Console.WriteLine($"{root}  {path}");
            stateStore.Save(state);
            return 0;
        }

        case "check":
        {
            int selfExit = await MaybeSelfUpdateAsync();
            if (selfExit >= 0) return selfExit;
            return await RunCheckAsync();
        }

        case "update":
        {
            int selfExit = await MaybeSelfUpdateAsync();
            if (selfExit >= 0) return selfExit;
            return await RunUpdateAsync();
        }

        case "ux":
        {
            Console.WriteLine($"MBXDist {AppInfo.Version} — MBXHub family updater");
            Console.WriteLine();
            int selfExit = await MaybeSelfUpdateAsync();
            if (selfExit >= 0) return selfExit;

            int checkExit = await RunCheckAsync();
            if (checkExit != 10)
            {
                Console.WriteLine();
                Console.WriteLine("Nothing to do.");
                return checkExit;
            }

            Console.WriteLine();
            Console.Write("Apply the recommended updates now? [y/N] ");
            var answer = yes ? "y" : Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Not applying. Re-run and answer 'y', or run: mbxdist update");
                return 10;
            }
            Console.WriteLine();
            return await RunUpdateAsync();
        }

        default:
            Console.Error.WriteLine($"MBXDist {AppInfo.Version}: unknown verb '{verb}'. Try: check | update | status | list | init | version");
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    await Emit("error", null, null, null, "error", ex.Message);
    return 1;
}

// ---------------- local functions ----------------

async Task<int> RunCheckAsync()
{
    var state = stateStore.Load();
    var diff = new Checker().Diff(feed.LoadCatalog(), state);

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(diff, FeedJson.Options));
    }
    else
    {
        foreach (var d in diff)
        {
            var label = d.Status switch
            {
                PackageStatus.UpToDate => "up-to-date",
                PackageStatus.UpdateAvailable => $"update available: {d.InstalledVersion} -> {d.RecommendedVersion}",
                PackageStatus.Ahead => $"ahead ({d.InstalledVersion} > {d.RecommendedVersion})",
                PackageStatus.Missing => $"not installed (recommended {d.RecommendedVersion})",
                _ => d.Status.ToString()
            };
            Console.WriteLine($"{d.Id,-45}  {label}");
        }
    }

    int actionable = diff.Count(d => d.Status is PackageStatus.UpdateAvailable or PackageStatus.Missing);
    await Emit("check", null, null, null, actionable > 0 ? "updates-available" : "up-to-date", $"{actionable} actionable");
    return actionable > 0 ? 10 : 0;
}

async Task<int> RunUpdateAsync()
{
    var state = stateStore.Load();
    var catalog = feed.LoadCatalog();
    var diff = new Checker().Diff(catalog, state);
    var actionable = diff
        .Where(d => d.Status is PackageStatus.UpdateAvailable or PackageStatus.Missing)
        .Select(d => d.Id)
        .ToList();

    if (actionable.Count == 0)
    {
        Console.WriteLine("Everything is up to date.");
        return 0;
    }

    var resolver = new DependencyResolver(id => feed.LoadManifest(id));
    var closure = resolver.ResolveClosure(actionable, state);

    var svc = new UpdateService(
        new HttpFeedClient(feedBase),
        new WindowsAuthenticodeCheck(),
        policy,
        new Updater(),
        Path.Combine(configDir, "staging"),
        state.TargetRoots);

    bool anyPending = false, anyVerifyFail = false, anyError = false;
    var nowIso = DateTime.UtcNow.ToString("o");

    foreach (var id in closure)
    {
        var manifest = feed.LoadManifest(id);
        var installed = state.Packages.TryGetValue(id, out var ps) ? ps.Version : null;
        var result = await svc.ApplyPackageAsync(manifest, state, nowIso);
        foreach (var r in result.Resources)
        {
            if (r.Error is not null)
            {
                anyError = true;
                if (r.Error.Contains("signature") || r.Error.Contains("sha256")) anyVerifyFail = true;
                Console.Error.WriteLine($"{id}  {r.Filename}: FAILED — {r.Error}");
            }
            else if (r.Applied == ApplyOutcome.StagedPending)
            {
                anyPending = true;
                Console.WriteLine($"{id}  {r.Filename}: staged pending — close the app holding it and re-run update");
            }
            else
            {
                Console.WriteLine($"{id}  {r.Filename}: updated");
            }
        }
        await Emit("apply", id, installed, manifest.Version,
            result.AllApplied ? "applied" : result.AnyPending ? "pending" : "failed",
            string.Join("; ", result.Resources.Where(r => r.Error is not null).Select(r => r.Error)));
    }

    stateStore.Save(state);

    if (anyVerifyFail) return 30;
    if (anyError) return 1;
    if (anyPending) return 20;
    return 0;
}

/// <summary>Best-effort self-update. Returns -1 to continue with the current binary, or an exit code
/// when the process should stop (a newer verified mbxdist was swapped in and re-exec'd, or verification failed).</summary>
async Task<int> MaybeSelfUpdateAsync()
{
    if (noSelfUpdate) return -1;

    var selfUpdater = new SelfUpdater(new HttpFeedClient(feedBase), new WindowsAuthenticodeCheck(), policy);
    var result = await selfUpdater.CheckAndStageAsync(AppInfo.Version, Path.Combine(configDir, "staging"));

    switch (result.Status)
    {
        case SelfUpdateStatus.Staged:
            Console.WriteLine($"MBXDist {result.LatestVersion} is available — updating self...");
            return SwapSelfAndRestart(result.StagedPath!);

        case SelfUpdateStatus.VerifyFailed:
            Console.Error.WriteLine($"self-update REFUSED: {result.Error}");
            await Emit("error", "halrad.mbxdist", AppInfo.Version, result.LatestVersion, "verify-failed", result.Error);
            return 30;

        default:
            return -1; // UpToDate or CheckFailed (offline / no marker yet): proceed with this binary
    }
}

int SwapSelfAndRestart(string stagedPath)
{
    var own = Environment.ProcessPath!;
    var old = own + ".old";
    if (File.Exists(old)) File.Delete(old);
    File.Move(own, old);
    File.Move(stagedPath, own);

    var psi = new ProcessStartInfo(own) { UseShellExecute = false };
    foreach (var a in args) psi.ArgumentList.Add(a);
    psi.ArgumentList.Add("--no-self-update");
    Process.Start(psi);
    return 0;
}

void CleanupOldSelfBinary()
{
    try
    {
        var old = Environment.ProcessPath + ".old";
        if (File.Exists(old)) File.Delete(old);
    }
    catch { /* still locked by the parent that just exited — next run gets it */ }
}

async Task Emit(string evt, string? package, string? fromV, string? toV, string result, string? detail)
{
    var state = stateStore.Load();
    await telemetry.EmitAsync(new TelemetryEvent
    {
        ClientId = string.IsNullOrEmpty(state.ClientId) ? "unregistered" : state.ClientId,
        Timestamp = DateTime.UtcNow.ToString("o"),
        Event = evt,
        Package = package,
        FromVersion = fromV,
        ToVersion = toV,
        Result = result,
        Detail = string.IsNullOrEmpty(detail) ? null : detail
    });
}

static string? GetFlagValue(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}
