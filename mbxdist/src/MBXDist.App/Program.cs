using System.Reflection;
using MBXDist.App;
using MBXDist.Core.Diff;
using MBXDist.Core.Embedded;
using MBXDist.Core.State;

// Entry point. Verb-driven; interactive console UX, `update`, self-update, and telemetry land in
// later increments. Exit codes: 0 = up-to-date/success, 10 = update available, 1 = error.

var feed = new EmbeddedFeed(Assembly.GetExecutingAssembly(), AppInfo.CatalogResourceSuffix, AppInfo.ManifestResourcePrefix);

var configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MBXDist");
var stateStore = new StateStore(Path.Combine(configDir, "mbxdist-state.json"));

string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "check";

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
            if (state.Packages.Count == 0)
            {
                Console.WriteLine("No packages recorded as installed.");
                return 0;
            }
            foreach (var (id, ps) in state.Packages)
                Console.WriteLine($"{id}  {ps.Version}");
            return 0;
        }

        case "check":
        {
            var diff = new Checker().Diff(feed.LoadCatalog(), stateStore.Load());
            var actionable = 0;
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
                if (d.Status is PackageStatus.UpdateAvailable or PackageStatus.Missing) actionable++;
                Console.WriteLine($"{d.Id}  {label}");
            }
            return actionable > 0 ? 10 : 0;
        }

        default:
            Console.Error.WriteLine($"MBXDist {AppInfo.Version}: unknown verb '{verb}'. Try: check | status | list | version");
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
