using MBXDist.Core.Model;
namespace MBXDist.App;

public sealed class CliOptions
{
    public string Verb { get; private set; } = "check";
    public List<string> Words { get; } = new();
    public bool Json { get; private set; }
    public bool DryRun { get; private set; }
    public bool Yes { get; private set; }
    public bool NoSelfUpdate { get; private set; }
    public string Feed { get; private set; } = AppInfo.DefaultFeedBaseUrl;
    public string Config { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MBXDist", "mbxdist-state.json");
    public string? FeedRoot { get; private set; }
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions(); var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var option = args[i].ToLowerInvariant();
            string Value()
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) throw new ArgumentException($"{option} requires a value");
                return args[++i];
            }
            switch (option)
            {
                case "--json": options.Json = true; break;
                case "--dry-run": options.DryRun = true; break;
                case "--yes": options.Yes = true; break;
                case "--no-self-update": options.NoSelfUpdate = true; break;
                case "--feed": options.Feed = Value(); break;
                case "--config": options.Config = Path.GetFullPath(Value()); break;
                case "--feed-root": options.FeedRoot = Path.GetFullPath(Value()); break;
                case "--version": positional.Add("version"); break;
                case "--help": case "-h": positional.Add("help"); break;
                default:
                    if (args[i].StartsWith('-')) throw new ArgumentException($"unknown option '{args[i]}'");
                    positional.Add(args[i]); break;
            }
        }
        if (!Uri.TryCreate(options.Feed, UriKind.Absolute, out var feed) || feed.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(feed.UserInfo))
            throw new ArgumentException("--feed must be an HTTP(S) URL without credentials");
        if (positional.Count > 0) { options.Verb = positional[0].ToLowerInvariant(); options.Words.AddRange(positional.Skip(1)); }
        else if (!Console.IsInputRedirected && !Console.IsOutputRedirected && !options.Json && !options.Yes && !options.DryRun) options.Verb = "ux";
        if (options.Verb == "ux" && positional.Count > 0)
            throw new ArgumentException("interactive mode is selected by running without a verb; use check or update for automation");
        if (options.Verb is not ("check" or "update" or "verify" or "repair" or "status" or "list" or "init" or "version" or "help" or "validate-feed" or "ux"))
            throw new ArgumentException($"unknown verb '{options.Verb}'");
        if (options.Verb == "init")
        { if (options.Words.Count is not (0 or 2)) throw new ArgumentException("usage: init <root> <absolute-path>"); }
        else if (options.Verb is not ("update" or "verify" or "repair") && options.Words.Count > 0)
            throw new ArgumentException($"{options.Verb} does not accept positional arguments");
        if (options.DryRun && options.Verb is not ("update" or "repair" or "check")) throw new ArgumentException("--dry-run is valid for update, repair or check");
        if (options.FeedRoot is not null && options.Verb != "validate-feed") throw new ArgumentException("--feed-root is only valid for validate-feed");
        if (options.Verb == "validate-feed" && options.FeedRoot is null) throw new ArgumentException("validate-feed requires --feed-root");
        return options;
    }
}
