// hello-app — a window MBXHub can start, and that can start MusicBee back.
//
// hello-charm teaches the contract and prints it. This one is the other half of the
// demonstration: something to LOOK at, and the two ways an application deals with a hub that
// is not running yet.
//
//   DELAY LOAD (default)     the window opens regardless and registration retries quietly in
//                            the background until a hub answers. Nothing blocks on MusicBee.
//   LOAD WITH LAUNCH (--start-musicbee)
//                            ask the Shell to start MusicBee first, then register.
//
// Both matter, and they are different products: an app whose own job needs the library should
// launch it; an app that merely integrates should never make a person's music player start
// because they opened something else.
//
// Why the start call is made from C# and not from the page: POST /meta/app/start is
// state-changing, so a browser-supplied Origin must be LAN-scoped. A WebView2 page fetching it
// sends Origin: null and is refused; a native caller sends none and is allowed. The button in
// the window therefore asks this process, which makes the call.
//
// One package (WebView2, the version MBXHub's own Shell pins). Every asset is local.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HelloApp;

internal static class Program
{
    private static string Host = "127.0.0.1";
    private static int Port = 8080;              // the hub's REST port
    private static bool Launched;                // the hub started us
    private static bool StartMusicBee;           // --start-musicbee: load WITH launch
    private static bool StartMinimized = true;   // --no-minimize turns it off

    /// <summary>The Shell listens on the odd port of the pair — REST even, Shell odd.
    /// Confirmed rather than assumed: <c>GET /meta/ping</c> answers
    /// <c>service: "MBXHub.Shell"</c> and names the hub it belongs to.</summary>
    private static int ShellPort => Port + 1;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    [STAThread]
    private static void Main(string[] args)
    {
        ParseArgs(args);
        ApplicationConfiguration.Initialize();
        Application.Run(new MarkWindow());
    }

    private sealed class MarkWindow : Form
    {
        private readonly WebView2 _view = new() { Dock = DockStyle.Fill };

        public MarkWindow()
        {
            Text = "Hello App — started by MBXHub";
            BackColor = Color.FromArgb(0x0f, 0x12, 0x16);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 780);
            MinimumSize = new Size(480, 420);
            KeyPreview = true;

            Controls.Add(_view);
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Load += async (_, _) => await Start();
        }

        private async Task Start()
        {
            if (!await ShowMark()) return;

            // LOAD WITH LAUNCH: before anything else, because the whole point is that the app
            // does not need the person to have opened MusicBee first.
            if (StartMusicBee) await StartMusicBeeThroughShell(StartMinimized);

            // DELAY LOAD: never blocks the window. It answers when a hub turns up, whether
            // that is now, in a minute, or never.
            _ = Task.Run(RegisterUntilAnswered);
        }

        private async Task<bool> ShowMark()
        {
            try
            {
                var profile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "hello-app", "webview");
                Directory.CreateDirectory(profile);

                var env = await CoreWebView2Environment.CreateAsync(null, profile);
                await _view.EnsureCoreWebView2Async(env);

                var core = _view.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;

                // The page asks; this process acts. See the Origin note at the top.
                core.WebMessageReceived += async (_, e) =>
                {
                    var msg = e.TryGetWebMessageAsString();
                    if (msg == "start-musicbee") await StartMusicBeeThroughShell(false);
                    else if (msg == "start-musicbee-minimized") await StartMusicBeeThroughShell(true);
                };

                var page = Path.Combine(AppContext.BaseDirectory, "app.html");
                var how = Launched ? "launched from MusicBee" : "run directly";
                core.Navigate(new UriBuilder(new Uri(page))
                {
                    Query = "how=" + Uri.EscapeDataString(how) +
                            "&mode=" + (StartMusicBee ? "launch" : "delay")
                }.Uri.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Controls.Remove(_view);
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    ForeColor = Color.Gainsboro,
                    Font = new Font("Segoe UI", 11f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "The WebView2 runtime is not installed, so the mark cannot be drawn."
                         + Environment.NewLine + Environment.NewLine
                         + "It ships with Microsoft Edge on Windows 10 and later."
                         + Environment.NewLine + Environment.NewLine + ex.Message
                });
                return false;
            }
        }

        private void Say(string state, string text)
        {
            var json = JsonSerializer.Serialize(new { state, text });
            try { _view.CoreWebView2?.PostWebMessageAsJson(json); } catch { }
        }

        /// <summary>
        /// Ask the Shell to start MusicBee. Three steps, and each failure is worth a different
        /// sentence to the person: is the Shell there, is it allowed to, did it work.
        /// </summary>
        private async Task StartMusicBeeThroughShell(bool minimized)
        {
            Say("working", "Looking for the MBXHub Shell…");

            // 1. Is that really the Shell? The pair convention (REST even, Shell odd) is a
            //    convention; /meta/ping is what makes it checkable, and its `service` is
            //    deliberately NOT "MBXHub" so a hub cannot be mistaken for a Shell.
            var (pingStatus, pingBody) = await Get($"http://{Host}:{ShellPort}/meta/ping");
            if (pingStatus != 200 || JsonNode.Parse(pingBody)?["service"]?.GetValue<string>() != "MBXHub.Shell")
            {
                Say("no", $"No MBXHub Shell on port {ShellPort}. It runs beside the hub; start it and try again.");
                return;
            }

            // 2. May it? canStart is the AND of permission and installation, so the reason is
            //    worth reading rather than guessing which of the two is missing.
            var (capStatus, capBody) = await Get($"http://{Host}:{ShellPort}/meta/app/start");
            var cap = JsonNode.Parse(capBody);
            if (capStatus == 200 && cap?["canStart"]?.GetValue<bool>() == false)
            {
                Say("no", $"The Shell will not start MusicBee: {cap?["reason"]}.");
                return;
            }

            // 3. Do it. No body, and Content-Length must be explicit — http.sys answers 411 to
            //    a POST that declares neither a length nor chunking, before the Shell sees it.
            // MINIMIZED IS OPT-IN AND IS THE POINT OF THE DEMO. Someone who opened a game and
            // wants their music running does not want a player stealing the screen — so the app
            // asks for it out of the way, and MusicBee comes up in the taskbar with the hub
            // live behind it. The route stays body-less by default; this is the one field.
            Say("working", minimized ? "Starting MusicBee, minimized…" : "Starting MusicBee…");
            var (status, body) = await Post($"http://{Host}:{ShellPort}/meta/app/start",
                                            minimized ? "{\"minimized\":true}" : null);
            if (status == 200)
                Say("yes", (minimized ? "MusicBee is starting out of the way. " : "MusicBee is starting. ")
                         + "Registration will answer as soon as the hub is up.");
            else
                Say("no", $"The Shell answered {status}: {Reason(body)}");
        }

        /// <summary>
        /// DELAY LOAD. Registration is not something to fail over: the hub may be closed now
        /// and open in ten minutes, and the window is worth looking at either way. Retries with
        /// a ceiling rather than forever — an app that polls a port for a week is a bug.
        /// </summary>
        private async Task RegisterUntilAnswered()
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (await TryRegister()) { Say("yes", "Registered with MBXHub. Approve it in the Charm Manager."); return; }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            Say("no", "No hub answered. Start MusicBee and reopen this window, or press the button above.");
        }

        private async Task<bool> TryRegister()
        {
            try
            {
                var manifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
                if (!File.Exists(manifestPath)) return false;

                var manifest = (JsonObject)JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;

                // THIS executable, always: the hub starts only the image it watched register,
                // so the value is never hand-typed.
                manifest["launch"] = Path.Combine(AppContext.BaseDirectory, "hello-app.exe") + " --launched";

                var content = new StringContent(manifest.ToJsonString(), Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var res = await Http.PostAsync($"http://{Host}:{Port}/charms/register", content);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }

    // ---- helpers -------------------------------------------------------------------------

    private static async Task<(int status, string body)> Get(string url)
    {
        try
        {
            var res = await Http.GetAsync(url);
            return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (0, ex.Message); }
    }

    private static async Task<(int status, string body)> Post(string url, string json = null)
    {
        try
        {
            // Explicitly empty, not absent: a POST with no Content-Length and no chunking is
            // refused by http.sys with 411 before it reaches the listener.
            var content = new StringContent(json ?? string.Empty, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var res = await Http.PostAsync(url, content);
            return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (0, ex.Message); }
    }

    private static string Reason(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            return node?["error"]?["message"]?.GetValue<string>()
                ?? node?["reason"]?.GetValue<string>()
                ?? body;
        }
        catch { return body; }
    }

    private static void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host" when i + 1 < args.Length: Host = args[++i]; break;
                case "--port" when i + 1 < args.Length: Port = int.Parse(args[++i]); break;
                case "--launched": Launched = true; break;
                case "--start-musicbee": StartMusicBee = true; break;
                case "--no-minimize": StartMinimized = false; break;
            }
        }
    }
}
