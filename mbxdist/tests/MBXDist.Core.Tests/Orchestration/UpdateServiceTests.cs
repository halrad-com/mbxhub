using MBXDist.Core.Apply;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;
using MBXDist.Core.Net;
using MBXDist.Core.Orchestration;
using MBXDist.Core.Verify;

namespace MBXDist.Core.Tests.Orchestration;

public class UpdateServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-svc-" + Guid.NewGuid().ToString("N"));
    private const string Thumb = "AABBCCDDEE";

    // Fake feed that writes fixed content for any url.
    private sealed class FakeFeed : IFeedClient
    {
        private readonly string _content;
        public FakeFeed(string content) => _content = content;
        public Task<string> GetTextAsync(string relativeUrl, CancellationToken ct = default) => Task.FromResult(_content);
        public async Task DownloadToAsync(string relativeUrl, string destPath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await File.WriteAllTextAsync(destPath, _content, ct);
        }
    }

    private sealed class FakeSig : ISignatureCheck
    {
        private readonly SignatureResult _r;
        public FakeSig(SignatureResult r) => _r = r;
        public SignatureResult Check(string filePath) => _r;
    }

    private string HashOf(string content)
    {
        var tmp = Path.Combine(_dir, "h.tmp");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(tmp, content);
        var h = Sha256.OfFile(tmp);
        File.Delete(tmp);
        return h;
    }

    private Manifest Manifest(string sha) => new()
    {
        Id = "halrad.mbxhub.core",
        Version = "0.5.4.6",
        Resources =
        {
            new ResourceEntry
            {
                Filename = "mb.dll", Url = "core/0.5.4.6/mb.dll", Sha256 = sha,
                Target = new TargetRef { Root = "plugins", Path = "mb.dll" }
            }
        }
    };

    private UpdateService Service(string content, SignatureResult sig) => new(
        new FakeFeed(content),
        new FakeSig(sig),
        new PinnedThumbprintPolicy(new[] { Thumb }),
        new Updater(),
        Path.Combine(_dir, "staging"),
        new Dictionary<string, string> { ["plugins"] = Path.Combine(_dir, "install") });

    [Fact]
    public async Task Happy_path_applies_and_records_state()
    {
        const string content = "COREBYTES";
        var svc = Service(content, new SignatureResult(true, Thumb));
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(Manifest(HashOf(content)), state, "2026-08-03T00:00:00Z");

        Assert.True(result.AllApplied);
        Assert.False(result.AnyFailed);
        Assert.Equal("0.5.4.6", state.Packages["halrad.mbxhub.core"].Version);
        Assert.Equal(content, File.ReadAllText(Path.Combine(_dir, "install", "mb.dll")));
    }

    [Fact]
    public async Task Sha_mismatch_fails_and_does_not_record()
    {
        var svc = Service("COREBYTES", new SignatureResult(true, Thumb));
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(Manifest("deadbeef"), state, "t");

        Assert.True(result.AnyFailed);
        Assert.Contains("sha256", result.Resources[0].Error);
        Assert.False(state.Packages.ContainsKey("halrad.mbxhub.core"));
        Assert.False(File.Exists(Path.Combine(_dir, "install", "mb.dll"))); // nothing applied
    }

    [Fact]
    public async Task Untrusted_signature_fails_closed()
    {
        const string content = "COREBYTES";
        var svc = Service(content, new SignatureResult(false, Thumb)); // not trusted
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(Manifest(HashOf(content)), state, "t");

        Assert.True(result.AnyFailed);
        Assert.False(File.Exists(Path.Combine(_dir, "install", "mb.dll")));
    }

    private Manifest ThirdPartyManifest(string sha) => new()
    {
        Id = "halrad.mbxhub.truedat.dependencies.ffmpeg",
        Version = "7.1",
        Resources =
        {
            new ResourceEntry
            {
                Filename = "ffmpeg.exe", Url = "ffmpeg/7.1/ffmpeg.exe", Sha256 = sha,
                Target = new TargetRef { Root = "plugins", Path = "ffmpeg.exe" },
                Authenticode = false   // third-party: we never sign it; sha256-only under the signed manifest
            }
        }
    };

    [Fact]
    public async Task Third_party_resource_applies_without_signature()
    {
        const string content = "FFMPEGBYTES";
        // Signature check reports untrusted/no-thumbprint — must not matter for authenticode:false.
        var svc = Service(content, new SignatureResult(false, null));
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(ThirdPartyManifest(HashOf(content)), state, "t");

        Assert.True(result.AllApplied);
        Assert.Equal("7.1", state.Packages["halrad.mbxhub.truedat.dependencies.ffmpeg"].Version);
    }

    [Fact]
    public async Task Third_party_resource_still_fails_on_sha_mismatch()
    {
        var svc = Service("FFMPEGBYTES", new SignatureResult(false, null));
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(ThirdPartyManifest("deadbeef"), state, "t");

        Assert.True(result.AnyFailed);
        Assert.Contains("sha256", result.Resources[0].Error);
    }

    [Fact]
    public async Task Unmapped_target_root_is_reported()
    {
        const string content = "COREBYTES";
        var svc = new UpdateService(
            new FakeFeed(content), new FakeSig(new SignatureResult(true, Thumb)),
            new PinnedThumbprintPolicy(new[] { Thumb }), new Updater(),
            Path.Combine(_dir, "staging"),
            new Dictionary<string, string>()); // no roots
        var state = new LocalState();

        var result = await svc.ApplyPackageAsync(Manifest(HashOf(content)), state, "t");

        Assert.True(result.AnyFailed);
        Assert.Contains("no install target", result.Resources[0].Error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
