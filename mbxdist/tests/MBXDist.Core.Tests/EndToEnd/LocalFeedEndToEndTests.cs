using System.Net;
using MBXDist.Core.Apply;
using MBXDist.Core.Diff;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.State;
using MBXDist.Core.Verify;

namespace MBXDist.Core.Tests.EndToEnd;

/// <summary>Full-pipeline test against a REAL local HTTP server: catalog diff -> dependency closure ->
/// HttpFeedClient download -> sha256 + signature policy -> lock-aware apply -> state record ->
/// idempotent second check. Signature check is a fake (Authenticode needs a signed binary; covered
/// separately); everything else is the production code path.</summary>
public class LocalFeedEndToEndTests : IAsyncLifetime, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-e2e-" + Guid.NewGuid().ToString("N"));
    private string FeedDir => Path.Combine(_dir, "feed");
    private string InstallDir => Path.Combine(_dir, "install");
    private string StagingDir => Path.Combine(_dir, "staging");
    private string StatePath => Path.Combine(_dir, "state.json");

    private const string Thumb = "AABBCCDDEE";
    private const string CoreContent = "MBXHUB-CORE-BYTES-0.5.4.6";

    private HttpListener _listener = null!;
    private string _baseUrl = null!;
    private Task? _serveLoop;

    private sealed class FakeSig : ISignatureCheck
    {
        public SignatureResult Check(string filePath) => new(true, Thumb);
    }

    public Task InitializeAsync()
    {
        // Lay out the static feed exactly like the server layout in the spec.
        var resDir = Path.Combine(FeedDir, "core", "0.5.4.6");
        Directory.CreateDirectory(resDir);
        File.WriteAllText(Path.Combine(resDir, "mb_MBXHub.dll"), CoreContent);

        // Serve it over a real HTTP socket.
        var port = FindFreePort();
        _baseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();
        _serveLoop = Task.Run(ServeAsync);
        return Task.CompletedTask;
    }

    private static int FindFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }

            var rel = ctx.Request.Url!.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(FeedDir, rel);
            if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path);
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(bytes);
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
            ctx.Response.Close();
        }
    }

    private Catalog Catalog() => new()
    {
        Packages = { new CatalogEntry { Id = "halrad.mbxhub.core", Version = "0.5.4.6" } }
    };

    private Manifest CoreManifest() => new()
    {
        Id = "halrad.mbxhub.core",
        Version = "0.5.4.6",
        Resources =
        {
            new ResourceEntry
            {
                Filename = "mb_MBXHub.dll",
                Url = "core/0.5.4.6/mb_MBXHub.dll",
                Sha256 = HashOfContent(CoreContent),
                Target = new TargetRef { Root = "musicbee-plugins", Path = "mb_MBXHub.dll" },
                Locked = true
            }
        }
    };

    private string HashOfContent(string content)
    {
        Directory.CreateDirectory(_dir);
        var tmp = Path.Combine(_dir, "hash.tmp");
        File.WriteAllText(tmp, content);
        var h = Sha256.OfFile(tmp);
        File.Delete(tmp);
        return h;
    }

    [Fact]
    public async Task Check_update_verify_recheck_full_pipeline()
    {
        var stateStore = new StateStore(StatePath);
        var state = stateStore.Load();
        state.TargetRoots["musicbee-plugins"] = InstallDir;

        // 1. CHECK: fresh machine -> core is missing.
        var diff1 = new Checker().Diff(Catalog(), state);
        Assert.Equal(PackageStatus.Missing, Assert.Single(diff1).Status);

        // 2. UPDATE: closure -> download over real HTTP -> verify -> apply -> record.
        var closure = new DependencyResolver(_ => CoreManifest()).ResolveClosure(new[] { "halrad.mbxhub.core" }, state);
        var svc = new UpdateService(
            new HttpFeedClient(_baseUrl),
            new FakeSig(),
            new PinnedThumbprintPolicy(new[] { Thumb }),
            new Updater(),
            StagingDir,
            state.TargetRoots);

        foreach (var id in closure)
        {
            var result = await svc.ApplyPackageAsync(CoreManifest(), state, "2026-08-03T00:00:00Z");
            Assert.True(result.AllApplied, string.Join("; ", result.Resources.Select(r => r.Error)));
        }
        stateStore.Save(state);

        Assert.Equal(CoreContent, File.ReadAllText(Path.Combine(InstallDir, "mb_MBXHub.dll")));

        // 3. VERIFY: installed files intact per recorded hashes.
        var reloaded = new StateStore(StatePath).Load();
        Assert.Empty(new Remediator().FindDamaged(reloaded.Packages["halrad.mbxhub.core"]));

        // 4. RE-CHECK: now up-to-date — the pipeline is idempotent.
        var diff2 = new Checker().Diff(Catalog(), reloaded);
        Assert.Equal(PackageStatus.UpToDate, Assert.Single(diff2).Status);

        // 5. Tamper the installed file -> verify catches it.
        File.WriteAllText(Path.Combine(InstallDir, "mb_MBXHub.dll"), "TAMPERED");
        var damaged = new Remediator().FindDamaged(reloaded.Packages["halrad.mbxhub.core"]);
        Assert.Equal(DamageKind.HashMismatch, Assert.Single(damaged).Kind);
    }

    public Task DisposeAsync()
    {
        try { _listener.Stop(); _listener.Close(); } catch { }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
