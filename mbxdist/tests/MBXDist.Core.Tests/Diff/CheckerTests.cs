using MBXDist.Core.Diff;
using MBXDist.Core.Model;

namespace MBXDist.Core.Tests.Diff;

public class CheckerTests
{
    private static Catalog Cat(params (string id, string ver)[] pkgs)
    {
        var c = new Catalog();
        foreach (var (id, ver) in pkgs) c.Packages.Add(new CatalogEntry { Id = id, Version = ver });
        return c;
    }

    private static LocalState State(params (string id, string ver)[] pkgs)
    {
        var s = new LocalState();
        foreach (var (id, ver) in pkgs) s.Packages[id] = new PackageState { Version = ver };
        return s;
    }

    [Fact]
    public void Missing_when_not_installed()
    {
        var d = new Checker().Diff(Cat(("halrad.mbxhub.core", "0.5.4.6")), State());
        Assert.Equal(PackageStatus.Missing, d[0].Status);
        Assert.Equal("0.5.4.6", d[0].RecommendedVersion);
        Assert.Null(d[0].InstalledVersion);
    }

    [Fact]
    public void UpdateAvailable_when_recommended_newer()
    {
        var d = new Checker().Diff(Cat(("halrad.mbxhub.core", "0.5.4.6")), State(("halrad.mbxhub.core", "0.5.4.5")));
        Assert.Equal(PackageStatus.UpdateAvailable, d[0].Status);
    }

    [Fact]
    public void UpToDate_when_equal()
    {
        var d = new Checker().Diff(Cat(("halrad.mbxhub.core", "0.5.4.6")), State(("halrad.mbxhub.core", "0.5.4.6")));
        Assert.Equal(PackageStatus.UpToDate, d[0].Status);
    }

    [Fact]
    public void Ahead_when_installed_newer_than_recommended()
    {
        var d = new Checker().Diff(Cat(("halrad.mbxhub.core", "0.5.4.5")), State(("halrad.mbxhub.core", "0.5.4.6")));
        Assert.Equal(PackageStatus.Ahead, d[0].Status);
    }
}
