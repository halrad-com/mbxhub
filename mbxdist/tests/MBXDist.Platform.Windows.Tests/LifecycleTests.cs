using MBXDist.App;
using MBXDist.Core.Apply;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.State;
using MBXDist.Core.Verify;

namespace MBXDist.Platform.Windows.Tests;

public class LifecycleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-lifecycle-" + Guid.NewGuid().ToString("N"));
    private string At(string name) => Path.Combine(_dir, name);
    public LifecycleTests() => Directory.CreateDirectory(_dir);
    private sealed class Signature : ISignatureCheck { public SignatureResult Check(string path) => new(false, null); }
    private sealed class Feed : IFeedClient
    {
        public bool Offline;
        public Task<string> GetTextAsync(string url, CancellationToken ct = default) => throw new IOException("offline");
        public Task DownloadToAsync(string url, string path, CancellationToken ct = default)
        {
            if (Offline) throw new IOException("offline");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "new"); return Task.CompletedTask;
        }
    }
    private Manifest M(string id, string filename)
    {
        File.WriteAllText(At("hash"), "new");
        return new Manifest
        {
            Id = id,
            Version = "1.0",
            Resources ={new ResourceEntry{Filename=filename,Url=id+"/1.0/"+filename,
            Sha256=Sha256.OfFile(At("hash")),Size=3,Authenticode=false,Target=new(){Root="install",Path=filename}}}
        };
    }
    private CliRunner Runner(Dictionary<string, Manifest> manifests, Feed feed) => new(
        new Catalog { Packages = manifests.Values.Select(m => new CatalogEntry { Id = m.Id, Version = m.Version }).ToList() },
        id => manifests[id], new Signature(), new PinnedThumbprintPolicy(Array.Empty<string>()), _ => feed);
    private CliOptions Options(params string[] args) => CliOptions.Parse(args.Concat(new[] { "--config", At("state.json"), "--no-self-update", "--json" }).ToArray());
    private StateStore Store()
    {
        var store = new StateStore(At("state.json"));
        store.Save(new LocalState { TargetRoots = { ["install"] = _dir } }); return store;
    }

    [Fact]
    public async Task Install_verify_tamper_repair_runs_through_command_orchestration()
    {
        var store = Store(); var manifest = M("app", "app.dll");
        var runner = Runner(new() { ["app"] = manifest }, new Feed());
        Assert.Equal(0, await runner.RunAsync(Options("update", "app"), Array.Empty<string>()));
        Assert.Equal(0, await runner.RunAsync(Options("verify"), Array.Empty<string>()));
        File.WriteAllText(At("app.dll"), "bad");
        Assert.Equal(10, await runner.RunAsync(Options("check"), Array.Empty<string>()));
        Assert.Equal(30, await runner.RunAsync(Options("verify"), Array.Empty<string>()));
        Assert.Equal(0, await runner.RunAsync(Options("repair"), Array.Empty<string>()));
        Assert.Equal("new", File.ReadAllText(At("app.dll")));
        Assert.Equal(0, await runner.RunAsync(Options("verify"), Array.Empty<string>()));
    }

    [Fact]
    public async Task Pending_dependency_blocks_parent_and_finishes_offline_on_retry()
    {
        var store = Store(); var app = M("app", "app.dll"); var dep = M("dep", "dep.dll");
        app.Dependencies.Add(new() { Id = "dep", MinVersion = "1.0" });
        File.WriteAllText(At("dep.dll"), "old"); var feed = new Feed();
        var runner = Runner(new() { ["app"] = app, ["dep"] = dep }, feed);
        using (var held = File.Open(At("dep.dll"), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            int code = await runner.RunAsync(Options("update", "app"), Array.Empty<string>());
            Assert.NotEqual(0, code); Assert.False(File.Exists(At("app.dll")));
            Assert.Equal("1.0", store.Load().InProgress["dep"]);
        }
        // Parent was deliberately never downloaded; provide its trusted bytes as pending to prove all-offline completion.
        File.WriteAllText(At("app.dll.pending"), "new"); feed.Offline = true;
        Assert.Equal(0, await runner.RunAsync(Options("update", "app"), Array.Empty<string>()));
        Assert.Empty(store.Load().InProgress);
        Assert.Equal("new", File.ReadAllText(At("dep.dll")));
        Assert.False(File.Exists(At("dep.dll.pending")));
    }

    [Fact]
    public async Task Handoff_returns_child_exit_status()
    {
        var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        File.Copy(command, At("old.exe")); File.Copy(command, At("next.exe"));
        Assert.Equal(30, await SelfSwap.RunAsync(At("next.exe"), At("old.exe"), new[] { "/c", "exit 30" }));
    }

    [Fact]
    public async Task Failed_handoff_restores_previous_executable()
    {
        var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        File.Copy(command, At("old.exe")); File.WriteAllText(At("next.exe"), "not an executable");
        var hash = Sha256.OfFile(At("old.exe"));
        await Assert.ThrowsAnyAsync<Exception>(() => SelfSwap.RunAsync(At("next.exe"), At("old.exe"), Array.Empty<string>()));
        Assert.Equal(hash, Sha256.OfFile(At("old.exe")));
    }

    [Fact]
    public void Overlapping_state_operations_are_refused()
    {
        var store = Store(); using var held = store.AcquireLock();
        Assert.Throws<IOException>(() => new StateStore(At("state.json")).AcquireLock());
    }

    [Fact]
    public async Task Two_packages_cannot_overwrite_the_same_target()
    {
        Store(); var first = M("first", "same.dll"); var second = M("second", "same.dll");
        var runner = Runner(new() { ["first"] = first, ["second"] = second }, new Feed());
        await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(Options("update", "first", "second"), Array.Empty<string>()));
        Assert.False(File.Exists(At("same.dll")));
    }

    [Fact]
    public async Task Implicit_dependency_cannot_replace_an_unavailable_interrupted_version()
    {
        var store = Store(); var app = M("app", "app.dll"); var dep = M("dep", "dep.dll");
        app.Dependencies.Add(new() { Id = "dep", MinVersion = "1.0" });
        File.WriteAllText(At("dep.dll"), "existing");
        var state = store.Load(); state.Packages["dep"] = new() { Version = "2.0" }; state.InProgress["dep"] = "3.0"; store.Save(state);
        var runner = Runner(new() { ["app"] = app, ["dep"] = dep }, new Feed());
        await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(Options("update", "app"), Array.Empty<string>()));
        Assert.Equal("existing", File.ReadAllText(At("dep.dll")));
        Assert.Equal("3.0", store.Load().InProgress["dep"]);
    }

    [Fact]
    public async Task Installed_package_targets_remain_reserved_when_not_selected()
    {
        Store(); var first = M("first", "same.dll"); var second = M("second", "same.dll");
        var runner = Runner(new() { ["first"] = first, ["second"] = second }, new Feed());
        Assert.Equal(0, await runner.RunAsync(Options("update", "first"), Array.Empty<string>()));
        await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(Options("update", "second"), Array.Empty<string>()));
        Assert.Equal("new", File.ReadAllText(At("same.dll")));
    }

    [Fact]
    public async Task Healthy_parent_does_not_hide_a_damaged_transitive_dependency()
    {
        Store(); var app = M("app", "app.dll"); var dep = M("dep", "dep.dll"); var basis = M("basis", "basis.dll");
        app.Dependencies.Add(new() { Id = "dep", MinVersion = "1.0" }); dep.Dependencies.Add(new() { Id = "basis", MinVersion = "1.0" });
        var feed = new Feed(); var runner = Runner(new() { ["app"] = app, ["dep"] = dep, ["basis"] = basis }, feed);
        Assert.Equal(0, await runner.RunAsync(Options("update", "app"), Array.Empty<string>()));
        File.WriteAllText(At("basis.dll"), "bad");
        Assert.Equal(0, await runner.RunAsync(Options("repair", "app"), Array.Empty<string>()));
        Assert.Equal("new", File.ReadAllText(At("basis.dll")));
    }

    [Fact]
    public async Task Same_package_pending_sidecar_cannot_be_another_resource_target()
    {
        Store(); var app = M("app", "app.dll");
        var second = M("unused", "second.dll").Resources[0]; second.Target.Path = "app.dll.pending";
        app.Resources.Add(second);
        var runner = Runner(new() { ["app"] = app }, new Feed());
        await Assert.ThrowsAnyAsync<Exception>(() => runner.RunAsync(Options("update", "app"), Array.Empty<string>()));
        Assert.False(File.Exists(At("app.dll")));
    }

    public void Dispose() => Directory.Delete(_dir, true);
}
