// hello-endpoint — a charm MBXHub calls, instead of one that calls MBXHub.
//
// hello-charm is a `proc` charm: it holds a WebSocket open and its events arrive on it.
// This is the other shape. An `endpoint` charm declares an ADDRESS, and when a person
// activates it the hub POSTs the event there. Nothing is held open, and the charm does
// not have to be running when the person clicks — if it is not, the delivery simply
// fails and the hub logs why.
//
// The one rule that shapes everything below: REGISTRATION IS LOOPBACK-ONLY, but the
// address you declare need not be loopback. The announcement has to come from the
// MusicBee machine; what answers can be anything on the same private network. That is
// the whole of the "local half / remote half" split in the SDK — this sample runs both
// halves in one process because that is the simplest thing that demonstrates it.
//
// Zero dependencies. Builds with `dotnet build`. Run it on the MusicBee machine.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HelloEndpoint;

internal static class Program
{
    // ---- settings you can change on the command line -----------------------------------
    private static string Host = "127.0.0.1";        // where the HUB is
    private static int Port = 8080;                  // the hub's REST port
    private static string ListenHost = "127.0.0.1";  // where WE answer  (--listen-host)
    private static int ListenPort = 9099;            //                  (--listen-port)
    private static bool Once;                        // --once : register and report, do not serve

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        ParseArgs(args);
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected console: plain text is fine */ }

        Banner("hello-endpoint");

        // 1. Start listening FIRST. The address goes in the manifest, so it has to be real
        //    before we announce it — and a hub that delivers to an address nobody is on
        //    gets a connection refused, which reads as a broken charm rather than a
        //    sample that had not got round to opening its socket yet.
        var listener = Step1_Listen();
        if (listener == null) return 1;

        // 2. Find the hub, and make sure it is one.
        if (!await Step2_FindHub()) return 1;

        // 3. Register, declaring that address.
        var manifest = LoadManifest();
        var reg = await Step3_Register(manifest);
        if (reg == null) return 1;

        // 4. Wait for a person to approve us. An endpoint charm needs the approval for a
        //    reason that has nothing to do with credentials: an unapproved registration is
        //    not drawn on any surface, so there is no menu entry to click.
        if (!await Step4_WaitForApproval(manifest, reg)) return Once ? 0 : 1;

        if (Once) { Note("--once: done. Nothing will be delivered while this is not running."); return 0; }

        // 5. Answer what the hub POSTs.
        await Step5_Serve(listener);
        return 0;
    }

    // ---- 1 ----------------------------------------------------------------------------
    private static HttpListener? Step1_Listen()
    {
        Heading("1. Open the address we are about to declare");
        var prefix = $"http://{ListenHost}:{ListenPort}/charm/";
        Say($"listening on {prefix}");
        try
        {
            var l = new HttpListener();
            l.Prefixes.Add(prefix);
            l.Start();
            Ok("open.");
            return l;
        }
        catch (HttpListenerException ex)
        {
            Fail($"could not listen on {prefix}: {ex.Message}");
            // 5 is ERROR_ACCESS_DENIED. Loopback is free to anyone; any other host is not.
            if (ex.ErrorCode == 5)
            {
                Note("Windows reserves non-loopback URL prefixes. Either keep the default 127.0.0.1,");
                Note($"or grant this one once, from an elevated prompt:");
                Note($"    netsh http add urlacl url={prefix} user=%USERNAME%");
            }
            else
                Note($"Another process may already be on port {ListenPort}. Pass --listen-port N.");
            return null;
        }
    }

    // ---- 2 ----------------------------------------------------------------------------
    private static async Task<bool> Step2_FindHub()
    {
        Heading("2. Find the hub");
        Say($"GET {Url("/ping")}");
        try
        {
            var node = JsonNode.Parse(await Http.GetStringAsync(Url("/ping")));
            var service = node?["data"]?["service"]?.GetValue<string>();
            if (service != "MBXHub")
            {
                Fail($"something answered on {Host}:{Port} but it is not MBXHub (service = {service ?? "null"}). Not registering.");
                return false;
            }
            Ok($"MBXHub is here. apiVersion {node?["data"]?["apiVersion"]}.");
            return true;
        }
        catch (Exception ex)
        {
            Fail($"no hub at {Host}:{Port}: {ex.Message}");
            Note("Is MusicBee running with the MBXHub plugin? Default port is 8080; pass --port N for another.");
            return false;
        }
    }

    // ---- 3 ----------------------------------------------------------------------------
    private static async Task<JsonNode?> Step3_Register(JsonObject manifest)
    {
        Heading("3. Register, declaring where we answer");
        Say($"POST {Url("/charms/register")}");
        Say(manifest.ToJsonString(Pretty), dim: true);

        var (status, body) = await Post("/charms/register", manifest.ToJsonString());
        var node = JsonNode.Parse(body);

        if (status == 403)
        {
            Fail("403 — the register route answers only callers on the MusicBee machine.");
            Note("The ADDRESS you declare may be elsewhere on your network; the ANNOUNCEMENT may not.");
            return null;
        }
        if (status != 200)
        {
            Fail($"{status} {ErrorLine(node)}");
            Note("The endpoint must be http or https on a private or loopback host: a public address is refused.");
            return null;
        }

        Ok($"registered. charmId {node?["data"]?["charmId"]}, status {node?["data"]?["status"]}");
        if (node?["data"]?["restartRequired"] is JsonNode rr)
            Note($"restartRequired: {rr.ToJsonString()} — the menu entry appears after MusicBee next starts.");
        return node;
    }

    // ---- 4 ----------------------------------------------------------------------------
    private static async Task<bool> Step4_WaitForApproval(JsonObject manifest, JsonNode reg)
    {
        Heading("4. Approval");
        var id = manifest["id"]!.GetValue<string>();

        if (reg["data"]?["status"]?.GetValue<string>() == "pending")
        {
            Note("Pending. A PERSON approves this at the console:");
            Note("    MusicBee -> MBXHub settings -> Charm Manager -> tick \"Hello Endpoint\" -> Approve");
            Note("An endpoint charm needs that for a reason that is not about credentials: nothing");
            Note("unapproved is drawn on a charm surface, so until then there is no entry to click.");
            if (Once) { Note("--once: not waiting."); return false; }
            Note("Polling GET /charms/{id} every 3 s ...");
        }

        while (true)
        {
            var (s, body) = await Get($"/charms/{Uri.EscapeDataString(id)}");
            var node = JsonNode.Parse(body);
            if (s != 200) { Fail($"{s} {ErrorLine(node)}"); return false; }

            var st = node?["data"]?["status"]?.GetValue<string>();
            if (st == "active")
            {
                Ok("approved.");
                // A ticket may have come back on the registration that followed the approval.
                // This sample never uses one: it makes no gated calls, it only RECEIVES. The
                // credential is for calling the hub, not for being called by it — see
                // hello-charm for the other half.
                Note("This sample makes no gated calls, so it needs no ticket. Being called is not a privilege.");
                return true;
            }
            if (st is "revoked" or "declined" or "banned")
            {
                Fail($"status {st}. Nothing will be delivered.");
                return false;
            }
            await Task.Delay(3000);
        }
    }

    // ---- 5 ----------------------------------------------------------------------------
    private static async Task Step5_Serve(HttpListener listener)
    {
        Heading("5. Receive activations");
        Note("Click it inside MusicBee: Tools -> MBXHub -> Hello Endpoint -> Ping the endpoint.");
        Note("If the entry is not there, MusicBee has not restarted since you registered — menus are read at start.");
        Note("Ctrl+C to stop. While this is not running, a click is an undelivered event and the hub log says so.");
        Console.WriteLine();

        while (listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch (Exception ex) { Fail($"listener stopped: {ex.Message}"); return; }

            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            // ANSWER FIRST, WORK AFTER. The hub gives the whole call 2 seconds and reads
            // only the status code — it never reads this body. A charm that does its work
            // before answering is a charm that times out, and a timed-out delivery is
            // reported as undelivered even though it arrived.
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = 0;
            ctx.Response.Close();

            var ev = JsonNode.Parse(body);
            var verb = ev?["event"]?.GetValue<string>();
            if (verb == "activated")
                Ok($"activated  placement={ev?["placement"]}  entry={ev?["entryIndex"]} \"{ev?["entryLabel"]}\"  at {ev?["atUtc"]}");
            else
                Say($"{verb ?? "(no event verb)"}: {body}", dim: true);
        }
    }

    // ---- helpers ------------------------------------------------------------------------
    private static JsonObject LoadManifest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        var m = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

        // The address is written here rather than in the file, because it depends on the
        // flags this run was given. It is the caller's word either way — the hub checks
        // only that it is http/https on a private or loopback host, never that anything
        // is actually there.
        m["endpoint"] = $"http://{ListenHost}:{ListenPort}/charm/";
        return m;
    }

    private static string Url(string path) => $"http://{Host}:{Port}{path}";

    private static async Task<(int status, string body)> Get(string path)
    {
        try
        {
            var res = await Http.GetAsync(Url(path));
            return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (0, $"{{\"error\":{{\"message\":\"{ex.Message}\"}}}}"); }
    }

    private static async Task<(int status, string body)> Post(string path, string json)
    {
        try
        {
            var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var res = await Http.PostAsync(Url(path), content);
            return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (0, $"{{\"error\":{{\"message\":\"{ex.Message}\"}}}}"); }
    }

    private static string ErrorLine(JsonNode? node)
    {
        var code = node?["error"]?["code"]?.GetValue<string>();
        var msg = node?["error"]?["message"]?.GetValue<string>();
        return code == null && msg == null ? "(no error body)" : $"{code}: {msg}";
    }

    private static void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host" when i + 1 < args.Length: Host = args[++i]; break;
                case "--port" when i + 1 < args.Length: Port = int.Parse(args[++i]); break;
                case "--listen-host" when i + 1 < args.Length: ListenHost = args[++i]; break;
                case "--listen-port" when i + 1 < args.Length: ListenPort = int.Parse(args[++i]); break;
                case "--once": Once = true; break;
                case "--help" or "-h" or "/?":
                    Console.WriteLine("hello-endpoint [--host H] [--port N] [--listen-host H] [--listen-port N] [--once]");
                    Environment.Exit(0);
                    break;
            }
        }
    }

    private static void Banner(string s) { Console.WriteLine(); Console.WriteLine("=== " + s + " ==="); }
    private static void Heading(string s) { Console.WriteLine(); Console.WriteLine("-- " + s); }
    private static void Say(string s, bool dim = false) { Console.WriteLine((dim ? "     " : "   ") + s); }
    private static void Ok(string s) => Console.WriteLine("   OK   " + s);
    private static void Fail(string s) => Console.WriteLine("   FAIL " + s);
    private static void Note(string s) => Console.WriteLine("        " + s);
}
