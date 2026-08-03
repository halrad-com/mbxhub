using System.Text.Json;
using MBXDist.Core.Model;

namespace MBXDist.Core.Tests.Model;

public class JsonRoundTripTests
{
    [Fact]
    public void Catalog_reads_camelCase()
    {
        const string json = """
        { "schemaVersion": 1, "packages": [ { "id": "halrad.mbxhub.core", "version": "0.5.4.6" } ] }
        """;
        var c = JsonSerializer.Deserialize<Catalog>(json, FeedJson.Options)!;
        Assert.Equal(1, c.SchemaVersion);
        Assert.Single(c.Packages);
        Assert.Equal("halrad.mbxhub.core", c.Packages[0].Id);
        Assert.Equal("0.5.4.6", c.Packages[0].Version);
    }

    [Fact]
    public void Manifest_reads_nested_resource_and_target()
    {
        const string json = """
        { "id": "halrad.mbxhub.truedat", "version": "1.2.0", "displayName": "TrueDat",
          "dependencies": [ { "id": "halrad.mbxhub.truedat.dependencies.ffmpeg", "minVersion": "7.0" } ],
          "resources": [ { "filename": "truedat.exe", "url": "truedat/1.2.0/truedat.exe",
            "sha256": "abc", "size": 42, "target": { "root": "truedat", "path": "truedat.exe" }, "locked": false } ] }
        """;
        var m = JsonSerializer.Deserialize<Manifest>(json, FeedJson.Options)!;
        Assert.Equal("halrad.mbxhub.truedat", m.Id);
        Assert.Single(m.Dependencies);
        Assert.Equal("7.0", m.Dependencies[0].MinVersion);
        var r = Assert.Single(m.Resources);
        Assert.Equal("truedat", r.Target.Root);
        Assert.Equal(42, r.Size);
        Assert.False(r.Locked);
    }

    [Fact]
    public void LocalState_round_trips()
    {
        var s = new LocalState
        {
            ClientId = "cid",
            TargetRoots = { ["truedat"] = "C:/x" },
            Packages = { ["halrad.mbxhub.core"] = new PackageState
            {
                Version = "0.5.4.5", InstalledAt = "2026-07-30T12:00:00Z",
                Resources = { new ResourceState { Filename = "mb_MBXHub.dll", Target = "C:/x/mb_MBXHub.dll", Sha256 = "h" } }
            } }
        };
        var json = JsonSerializer.Serialize(s, FeedJson.Options);
        var back = JsonSerializer.Deserialize<LocalState>(json, FeedJson.Options)!;
        Assert.Equal("cid", back.ClientId);
        Assert.Equal("C:/x", back.TargetRoots["truedat"]);
        Assert.Equal("0.5.4.5", back.Packages["halrad.mbxhub.core"].Version);
        Assert.Equal("h", back.Packages["halrad.mbxhub.core"].Resources[0].Sha256);
    }
}
