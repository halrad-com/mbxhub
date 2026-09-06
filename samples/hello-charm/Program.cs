// hello-charm — the smallest complete MBXHub charm.
//
// Read top to bottom: Main is the whole story, one numbered step per method.
// Every call the hub makes, every answer it gives, and every refusal it can give is
// printed as it happens, so you can watch the contract rather than read about it.
//
// Zero dependencies. Builds with `dotnet build`. Runs on the MusicBee machine
// (registration is loopback-only, by design).

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HelloCharm;

internal static class Program
{
    // ---- settings you can change on the command line -----------------------------------
    private static string Host = "127.0.0.1";
    private static int Port = 8080;
    private static string? ProxyTarget;      // --proxy http://<lan-device>/... : the prompt demo
    private static bool Launched;            // set by the hub when it starts us (see manifest launch)
    private static bool Once;                // --once : register and report, do not wait around
    private static bool Forget;              // --forget : throw away the stored ticket first

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private const string TicketHeader = "X-MBXHub-Ticket";

    private static async Task<int> Main(string[] args)
    {
        ParseArgs(args);
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected or unusual console: plain text is fine */ }

        Banner("hello-charm" + (Launched ? "  (started by MBXHub)" : ""));

        // 1. Find the hub, and make sure it is one.
        var ping = await Step1_FindHub();
        if (ping == null) return 1;

        // 2. Register. Pending is the normal first answer.
        var manifest = LoadManifest();
        var reg = await Step2_Register(manifest);
        if (reg == null) return 1;

        // 3. The open routes work whether or not anyone has approved us.
        await Step3_OpenCall();

        // 4. Wait for a person to approve us, then collect the ticket.
        var ticket = await Step4_WaitForApprovalAndCollectTicket(manifest, reg);
        if (ticket == null) return Once ? 0 : 1;

        // 5. Gated calls: one we asked for, one we did not, and (optionally) one that asks the person.
        await Step5_GatedCalls(ticket);

        if (Once) { Note("--once: done."); return 0; }

        // 6. Receive events: bind the hub's WebSocket with the ticket and print what arrives.
        await Step6_ListenForEvents(ticket);
        return 0;
    }

    // ---- 1 ----------------------------------------------------------------------------
    private static async Task<JsonNode?> Step1_FindHub()
    {
        Heading("1. Find the hub");
        Say($"GET {Url("/ping")}");
        try
        {
            var body = await Http.GetStringAsync(Url("/ping"));
            var node = JsonNode.Parse(body);
            var service = node?["data"]?["service"]?.GetValue<string>();
            var api = node?["data"]?["apiVersion"]?.GetValue<string>();
            if (service != "MBXHub")
            {
                Fail($"something answered on {Host}:{Port} but it is not MBXHub (service = {service ?? "null"}). Not registering.");
                return null;
            }
            Ok($"MBXHub is here. apiVersion {api}. Remember this port; do not probe again.");
            return node;
        }
        catch (Exception ex)
        {
            Fail($"no hub at {Host}:{Port}: {ex.Message}");
            Note("Is MusicBee running with the MBXHub plugin? Default port is 8080; pass --port N for another.");
            return null;
        }
    }

    // ---- 2 ----------------------------------------------------------------------------
    private static async Task<JsonNode?> Step2_Register(JsonObject manifest)
    {
        Heading("2. Register");
        Say($"POST {Url("/charms/register")}");
        Say(manifest.ToJsonString(Pretty), dim: true);

        var (status, body) = await Post("/charms/register", manifest.ToJsonString(), ticket: null);
        var node = JsonNode.Parse(body);

        if (status == 403)
        {
            Fail("403 — the register route answers only callers on the MusicBee machine. Run this sample there.");
            return null;
        }
        if (status != 200)
        {
            Fail($"{status} {ErrorLine(node)}");
            Note("UNKNOWN_SCOPE = a scope in the manifest is not one the hub knows (GET /charms/capabilities lists them).");
            Note("ID_CLAIMED    = this id is registered by a different publisher; pick your own id.");
            return null;
        }

        var st = node?["data"]?["status"]?.GetValue<string>();
        var id = node?["data"]?["charmId"]?.GetValue<string>();
        Ok($"registered. charmId {id}, status {st}, apiVersion {node?["data"]?["apiVersion"]}");
        if (node?["data"]?["restartRequired"] is JsonNode rr)
            Note($"restartRequired: {rr.ToJsonString()} — those surfaces appear after MusicBee next starts.");
        else
            Note("restartRequired: absent — this hub predates the field (every current build answers it on every 200). Update the hub.");
        return node;
    }

    // ---- 3 ----------------------------------------------------------------------------
    private static async Task Step3_OpenCall()
    {
        Heading("3. An open route, no ticket");
        Say($"GET {Url("/nowplaying")}");
        try
        {
            var body = await Http.GetStringAsync(Url("/nowplaying"));
            Ok("answered. Pending or not, the open routes are yours exactly as they are anyone's.");
            Say(Truncate(body, 200), dim: true);
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    // ---- 4 ----------------------------------------------------------------------------
    private static async Task<string?> Step4_WaitForApprovalAndCollectTicket(JsonObject manifest, JsonNode reg)
    {
        Heading("4. Approval and the ticket");
        var id = manifest["id"]!.GetValue<string>();

        if (Forget && File.Exists(TicketPath())) { File.Delete(TicketPath()); Note("--forget: stored ticket discarded."); }

        // A ticket we were given on an earlier run. Still good? One gated call tells us.
        if (File.Exists(TicketPath()))
        {
            var stored = File.ReadAllText(TicketPath()).Trim();
            var (s, _) = await Get("/aria/status", stored);
            if (s == 200) { Ok($"using the ticket stored at {TicketPath()} (still valid)."); return stored; }
            Note($"stored ticket answered {s} — it was revoked or the hub was reset. Discarding it and starting over.");
            File.Delete(TicketPath());
        }

        // Did the registration itself already hand us a ticket? (Only on the first register after an approval.)
        var t = reg["data"]?["ticket"]?.GetValue<string>();
        if (t != null) { Store(t); Ok("the registration answer carried the ticket."); return t; }
        if (ExplainWithheldTicket(reg)) return null;

        var status = reg["data"]?["status"]?.GetValue<string>();
        if (status == "pending")
        {
            Note("Pending. A PERSON approves this at the console:");
            Note("    MusicBee -> MBXHub settings -> Charm Manager -> tick \"Hello Charm\" -> Approve");
            Note("Until then this sample keeps working on the open routes. Polling GET /charms/{id} every 3 s ...");
            if (Once) { Note("--once: not waiting."); return null; }
        }

        while (true)
        {
            var (s, body) = await Get($"/charms/{Uri.EscapeDataString(id)}", ticket: null);
            var node = JsonNode.Parse(body);
            var st = node?["data"]?["status"]?.GetValue<string>();
            if (s != 200) { Fail($"{s} {ErrorLine(node)}"); return null; }

            if (st == "active")
            {
                Ok($"approved. tier {node?["data"]?["tier"]}, granted {node?["data"]?["grantedScopes"]?.ToJsonString()}");
                // The ticket travels once, on the registration that FOLLOWS the approval.
                var (_, again) = await Post("/charms/register", manifest.ToJsonString(), ticket: null);
                var againNode = JsonNode.Parse(again);
                var ticket = againNode?["data"]?["ticket"]?.GetValue<string>();
                if (ticket == null)
                {
                    // Two answers look alike here - active, no ticket - and their remedies are opposite.
                    // `ticketWithheld` is present only when the hub is holding it back on purpose.
                    if (!ExplainWithheldTicket(againNode))
                        Fail("active, but no ticket came back and none was withheld. It is delivered exactly once; if a previous run collected it and lost it, revoke and approve again in the Charm Manager.");
                    return null;
                }
                Store(ticket);
                Ok($"ticket received and stored at {TicketPath()}. It never goes in a URL, only in the {TicketHeader} header, only over loopback.");
                return ticket;
            }
            if (st is "revoked" or "declined" or "banned")
            {
                Fail($"status {st}. Nothing more to do; the open routes still work.");
                return null;
            }
            await Task.Delay(3000);
        }
    }

    // ---- 5 ----------------------------------------------------------------------------
    /// <summary>
    /// An active answer with no ticket may carry <c>ticketWithheld</c>: the hub decided not to hand it over
    /// THIS time, and the value says why. Without the field, no ticket means it was already collected.
    /// Returns true when a reason was present and explained.
    /// </summary>
    private static bool ExplainWithheldTicket(JsonNode? answer)
    {
        var why = answer?["data"]?["ticketWithheld"]?.GetValue<string>();
        if (why == null) return false;
        if (why == "caller-unresolved")
            Fail("active, but the hub could not identify this process, so it kept the ticket. Register again from the same executable; the next answer whose caller resolves delivers it.");
        else
            Fail($"active, but the hub withheld the ticket: {why}. Register again; if it persists, look in the hub log.");
        return true;
    }

    private static async Task Step5_GatedCalls(string ticket)
    {
        Heading("5. Gated calls with the ticket");

        Say($"GET {Url("/aria/status")}   (scope automation:status — we asked for it)");
        var (s1, b1) = await Get("/aria/status", ticket);
        if (s1 == 200) Ok("200 — granted."); else Fail($"{s1} {ErrorLine(JsonNode.Parse(b1))}");

        Say($"GET {Url("/aria/presets")}  (scope automation:presets — we did NOT ask for it)");
        var (s2, b2) = await Get("/aria/presets", ticket);
        var n2 = JsonNode.Parse(b2);
        if (s2 == 403)
            Ok($"403 as expected. state = {n2?["error"]?["state"]}, capability = {n2?["error"]?["capability"]}. The refusal names the scope; asking for it in the manifest is the remedy.");
        else
            Fail($"expected 403, got {s2} {ErrorLine(n2)}");

        if (ProxyTarget != null)
        {
            Say($"POST {Url("/api/proxy")} -> {ProxyTarget}   (scope proxy:lan — an OUTWARD-acting call)");
            Note("If this charm is unsigned and prompts are on, the person at the MusicBee machine is asked now: Not now / Trust. This call waits for the answer.");
            var body = JsonSerializer.Serialize(new { method = "GET", url = ProxyTarget });
            var (s3, b3) = await Post("/api/proxy", body, ticket);
            var n3 = JsonNode.Parse(b3);
            if (s3 == 200) Ok("200 — the person said Trust (or it was already granted).");
            else Ok($"{s3} state = {n3?["error"]?["state"]} capability = {n3?["error"]?["capability"]} — 'Not now' is not an error; it is the person's answer.");
        }
        else
            Note("(--proxy http://<lan-device>/path adds the outward call that raises the runtime prompt.)");
    }

    // ---- 6 ----------------------------------------------------------------------------
    private static async Task Step6_ListenForEvents(string ticket)
    {
        Heading("6. Receive events");
        var wsUrl = new Uri($"ws://{Host}:{Port}/");
        Say($"connect {wsUrl} and send {{\"bind\":\"<ticket>\"}}");
        using var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(wsUrl, CancellationToken.None);
            var bind = JsonSerializer.Serialize(new { bind = ticket });
            await ws.SendAsync(Encoding.UTF8.GetBytes(bind), WebSocketMessageType.Text, true, CancellationToken.None);
            Ok("bound. A bound socket gets this charm's own events (CharmActivated) regardless of subscriptions.");
            Note("Click the charm's menu entry inside MusicBee (Tools -> MBXHub -> Hello Charm -> Say hello) and the event lands here.");
            Note("If the entry is not there: MusicBee has not restarted since you registered (menus are read at start), or the hub predates menu placement. Ctrl+C to stop.");

            var buf = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(buf, CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close) { Note("socket closed by the hub."); return; }
                    sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                } while (!r.EndOfMessage);

                var frame = sb.ToString();
                var ev = JsonNode.Parse(frame)?["event"]?.GetValue<string>();
                if (ev == "CharmActivated")
                    Ok($"CharmActivated -> {frame}");
                else
                    Say($"{ev}: {Truncate(frame, 160)}", dim: true);
            }
        }
        catch (Exception ex) { Fail($"WebSocket: {ex.Message}"); }
    }

    // ---- helpers ------------------------------------------------------------------------
    private static JsonObject LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        var m = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

        // The launch target must be THIS executable — the hub compares it to the image it
        // watched register and refuses anything else. Filled here, never hand-typed.
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
        m["launch"] = exe + " --launched";
        // The menu entry's action stays the bare verb "launch": what runs is always the
        // manifest's `launch` field above, and a target written after the verb is ignored.
        return m;
    }

    private static async Task<(int status, string body)> Get(string path, string? ticket)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Url(path));
        if (ticket != null) req.Headers.TryAddWithoutValidation(TicketHeader, ticket);
        using var res = await Http.SendAsync(req);
        return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private static async Task<(int status, string body)> Post(string path, string json, string? ticket)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Url(path))
        {
            Content = new StringContent(json, Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (ticket != null) req.Headers.TryAddWithoutValidation(TicketHeader, ticket);
        using var res = await Http.SendAsync(req);
        return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private static string Url(string path) => $"http://{Host}:{Port}{path}";

    // One ticket per hub. A hub is identified here by where we reached it; a real app should
    // key by the hub's own identity if it talks to more than one.
    private static string TicketPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hello-charm");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"ticket-{Host}-{Port}.txt");
    }

    private static void Store(string ticket) => File.WriteAllText(TicketPath(), ticket);

    private static string ErrorLine(JsonNode? n)
    {
        var e = n?["error"];
        if (e == null) return "";
        var s = $"{e["code"]}: {e["message"]}";
        if (e["state"] != null) s += $" (state {e["state"]}, capability {e["capability"]})";
        return s;
    }

    private static void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host": Host = args[++i]; break;
                case "--port": Port = int.Parse(args[++i]); break;
                case "--proxy": ProxyTarget = args[++i]; break;
                case "--launched": Launched = true; break;
                case "--once": Once = true; break;
                case "--forget": Forget = true; break;
                case "-h": case "--help":
                    Console.WriteLine("hello-charm [--host 127.0.0.1] [--port 8080] [--once] [--forget] [--proxy http://<lan-device>/path]");
                    Environment.Exit(0); break;
            }
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private static void Banner(string s) { Console.WriteLine(); Console.WriteLine($"== {s} =="); }
    private static void Heading(string s) { Console.WriteLine(); Console.WriteLine($"--- {s}"); }
    private static void Say(string s, bool dim = false)
    {
        if (dim) Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    " + s.Replace("\n", "\n    "));
        Console.ResetColor();
    }
    private static void Ok(string s) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("  + " + s); Console.ResetColor(); }
    private static void Fail(string s) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("  x " + s); Console.ResetColor(); }
    private static void Note(string s) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("  . " + s); Console.ResetColor(); }
}
