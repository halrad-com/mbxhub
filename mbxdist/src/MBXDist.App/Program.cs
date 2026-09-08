using System.Reflection;
using System.Text.Json;
using MBXDist.App;
using MBXDist.Core.Embedded;
using MBXDist.Core.Model;
using MBXDist.Core.Verify;
using MBXDist.Platform.Windows;

bool json = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
try
{
    var options = CliOptions.Parse(args);
    var feed = new EmbeddedFeed(Assembly.GetExecutingAssembly(), AppInfo.CatalogResourceSuffix, AppInfo.ManifestResourcePrefix);
    var runner = new CliRunner(feed.LoadCatalog(), feed.LoadManifest, new WindowsAuthenticodeCheck(), new PinnedThumbprintPolicy(AppInfo.PinnedThumbprints));
    return await runner.RunAsync(options, args);
}
catch (Exception ex)
{
    int code = ex is VerificationException ? 30 : 1;
    if (json) Console.WriteLine(JsonSerializer.Serialize(new { exitCode = code, error = ex.Message }, FeedJson.Options));
    else Console.Error.WriteLine($"error: {ex.Message}");
    return code;
}
