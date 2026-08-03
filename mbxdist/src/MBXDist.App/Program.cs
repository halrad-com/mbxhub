using System.Reflection;
using MBXDist.App;
using MBXDist.Core.Embedded;

// Entry point. Verb-driven; interactive console UX and the full verb set land in later increments.
var feed = new EmbeddedFeed(Assembly.GetExecutingAssembly(), AppInfo.CatalogResourceSuffix, AppInfo.ManifestResourcePrefix);

string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "check";

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

    default:
        Console.WriteLine($"MBXDist {AppInfo.Version}");
        Console.WriteLine($"(verb '{verb}' not yet wired — coming in the next build increment)");
        return 0;
}
