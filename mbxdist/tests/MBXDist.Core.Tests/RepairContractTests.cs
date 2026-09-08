using MBXDist.Core.Apply;
using MBXDist.Core.Diff;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.State;
using MBXDist.Core.Verify;

namespace MBXDist.Core.Tests;

public class RepairContractTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-contract-" + Guid.NewGuid().ToString("N"));
    private string At(string name) => Path.Combine(_dir, name);
    public RepairContractTests() => Directory.CreateDirectory(_dir);
    private sealed class Feed : IFeedClient
    {
        public bool Offline { get; set; }
        public Task<string> GetTextAsync(string url, CancellationToken ct = default) => throw new IOException("offline");
        public Task DownloadToAsync(string url, string path, CancellationToken ct = default)
        {
            if (Offline) throw new IOException("offline");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "new");
            return Task.CompletedTask;
        }
    }
    private sealed class Signature : ISignatureCheck
    {
        public SignatureResult Check(string path) => new(false, null);
    }
    private Manifest Manifest()
    {
        File.WriteAllText(At("hash"), "new");
        return new Manifest
        {
            Id = "app",
            Version = "1.0",
            Resources = {
            new ResourceEntry { Filename="a.dll", Url="app/1.0/a.dll", Sha256=Sha256.OfFile(At("hash")), Size=3,
                Authenticode=false, Target=new TargetRef { Root="install", Path="a.dll" } }
        }
        };
    }
    private UpdateService Service(Feed feed) => new(feed, new Signature(), new PinnedThumbprintPolicy(Array.Empty<string>()),
        new Updater(), At("staging"), new Dictionary<string, string> { ["install"] = _dir });

    [Fact]
    public async Task Verified_pending_file_finishes_without_a_download()
    {
        var manifest = Manifest();
        File.WriteAllText(At("a.dll.pending"), "new");
        var result = await Service(new Feed { Offline = true }).ApplyPackageAsync(manifest, new LocalState(), "now");
        Assert.True(result.AllApplied);
        Assert.Equal("new", File.ReadAllText(At("a.dll")));
        Assert.False(File.Exists(At("a.dll.pending")));
    }

    [Fact]
    public async Task Bad_later_resource_does_not_modify_first_target()
    {
        var manifest = Manifest();
        manifest.Resources.Add(new ResourceEntry
        {
            Filename = "b.dll",
            Url = "app/1.0/b.dll",
            Sha256 = new string('a', 64),
            Size = 3,
            Authenticode = false,
            Target = new TargetRef { Root = "install", Path = "b.dll" }
        });
        File.WriteAllText(At("a.dll"), "old");
        var result = await Service(new Feed()).ApplyPackageAsync(manifest, new LocalState(), "now");
        Assert.True(result.AnyFailed);
        Assert.Equal("old", File.ReadAllText(At("a.dll")));
    }

    [Fact]
    public async Task Size_mismatch_is_rejected_even_when_hash_matches()
    {
        var manifest = Manifest(); manifest.Resources[0].Size = 40;
        var result = await Service(new Feed()).ApplyPackageAsync(manifest, new LocalState(), "now");
        Assert.True(result.AnyFailed);
        Assert.False(File.Exists(At("a.dll")));
    }

    [Fact]
    public async Task Relative_escape_target_is_rejected_before_application()
    {
        var manifest = Manifest(); manifest.Resources[0].Target.Path = "../escape.dll";
        // Keep even the deliberately unsafe old behavior inside this test's scratch directory.
        var service = new UpdateService(new Feed(), new Signature(), new PinnedThumbprintPolicy(Array.Empty<string>()),
            new Updater(), At("staging"), new Dictionary<string, string> { ["install"] = At("inside") });
        var result = await service.ApplyPackageAsync(manifest, new LocalState(), "now");
        Assert.True(result.AnyFailed);
    }

    [Fact]
    public void Dependency_is_ordered_before_parent()
    {
        var parent = new Manifest { Id = "app", Version = "1.0", Dependencies = { new Dependency { Id = "dep", MinVersion = "1.0" } } };
        var resolver = new DependencyResolver(id => id == "app" ? parent : new Manifest { Id = "dep", Version = "1.0" });
        Assert.Equal(new[] { "dep", "app" }, resolver.ResolveClosure(new[] { "app" }, new LocalState()));
    }

    [Fact]
    public void Unavailable_dependency_minimum_is_rejected()
    {
        var parent = new Manifest { Id = "app", Version = "1.0", Dependencies = { new Dependency { Id = "dep", MinVersion = "2.0" } } };
        var resolver = new DependencyResolver(id => id == "app" ? parent : new Manifest { Id = "dep", Version = "1.0" });
        Assert.Throws<InvalidDataException>(() => resolver.ResolveClosure(new[] { "app" }, new LocalState()));
    }

    [Fact]
    public void Interrupted_state_write_recovers_previous_saved_state()
    {
        var store = new StateStore(At("state.json"));
        store.Save(new LocalState { ClientId = "first" });
        store.Save(new LocalState { ClientId = "second" });
        File.WriteAllText(At("state.json"), "{");
        Assert.Equal("first", store.Load().ClientId);
    }

    [Fact]
    public async Task Interrupted_multi_resource_apply_resumes_from_verified_staging_offline()
    {
        var manifest = Manifest();
        manifest.Resources.Add(new ResourceEntry
        {
            Filename = "b.dll",
            Url = "app/1.0/b.dll",
            Sha256 = manifest.Resources[0].Sha256,
            Size = 3,
            Authenticode = false,
            Target = new() { Root = "install", Path = "b.dll" }
        });
        Directory.CreateDirectory(At("b.dll")); // real placement failure after the first file is applied
        var store = new StateStore(At("state.json")); var state = new LocalState(); var feed = new Feed();
        var result = await Service(feed).ApplyPackageAsync(manifest, state, "now", persist: () => store.Save(state));
        Assert.True(result.AnyFailed);
        Assert.Equal("new", File.ReadAllText(At("a.dll")));
        Assert.Empty(store.Load().Packages);
        Assert.Equal("1.0", store.Load().InProgress["app"]);
        Directory.Delete(At("b.dll")); feed.Offline = true;
        var recovered = store.Load();
        result = await Service(feed).ApplyPackageAsync(manifest, recovered, "later", persist: () => store.Save(recovered));
        Assert.True(result.AllApplied);
        Assert.Equal("new", File.ReadAllText(At("b.dll")));
        Assert.Empty(store.Load().InProgress);
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
