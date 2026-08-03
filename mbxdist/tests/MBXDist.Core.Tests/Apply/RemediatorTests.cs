using MBXDist.Core.Apply;
using MBXDist.Core.Integrity;
using MBXDist.Core.Model;

namespace MBXDist.Core.Tests.Apply;

public class RemediatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-rem-" + Guid.NewGuid().ToString("N"));

    private string Write(string name, string content)
    {
        Directory.CreateDirectory(_dir);
        var p = Path.Combine(_dir, name);
        File.WriteAllText(p, content);
        return p;
    }

    private static PackageState PkgWith(params ResourceState[] rs)
    {
        var ps = new PackageState { Version = "1.0" };
        ps.Resources.AddRange(rs);
        return ps;
    }

    [Fact]
    public void Intact_resource_is_not_reported()
    {
        var path = Write("ok.bin", "abc");
        var ps = PkgWith(new ResourceState { Filename = "ok.bin", Target = path, Sha256 = Sha256.OfFile(path) });
        Assert.Empty(new Remediator().FindDamaged(ps));
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        var ps = PkgWith(new ResourceState { Filename = "gone.bin", Target = Path.Combine(_dir, "gone.bin"), Sha256 = "deadbeef" });
        var d = new Remediator().FindDamaged(ps);
        Assert.Equal(DamageKind.Missing, Assert.Single(d).Kind);
    }

    [Fact]
    public void Tampered_file_is_reported_as_hash_mismatch()
    {
        var path = Write("tampered.bin", "abc");
        var recordedHash = Sha256.OfFile(path);
        File.WriteAllText(path, "xyz"); // change on disk after recording
        var ps = PkgWith(new ResourceState { Filename = "tampered.bin", Target = path, Sha256 = recordedHash });
        var d = new Remediator().FindDamaged(ps);
        Assert.Equal(DamageKind.HashMismatch, Assert.Single(d).Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
