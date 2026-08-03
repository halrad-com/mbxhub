using System.Reflection;
using MBXDist.Core.Embedded;

namespace MBXDist.Core.Tests.Embedded;

public class EmbeddedFeedTests
{
    private static EmbeddedFeed Feed() =>
        new(Assembly.GetExecutingAssembly(), "catalog.json", "manifests.");

    [Fact]
    public void LoadCatalog_reads_embedded_catalog()
    {
        var c = Feed().LoadCatalog();
        Assert.Single(c.Packages);
        Assert.Equal("halrad.mbxhub.core", c.Packages[0].Id);
        Assert.Equal("0.5.4.6", c.Packages[0].Version);
    }

    [Fact]
    public void LoadManifest_reads_manifest_by_package_id()
    {
        var m = Feed().LoadManifest("halrad.mbxhub.core");
        Assert.Equal("MBXHub", m.DisplayName);
        Assert.True(m.Resources[0].Locked);
        Assert.Equal("musicbee-plugins", m.Resources[0].Target.Root);
    }

    [Fact]
    public void LoadManifest_unknown_id_throws()
    {
        Assert.Throws<FileNotFoundException>(() => Feed().LoadManifest("halrad.mbxhub.nope"));
    }
}
