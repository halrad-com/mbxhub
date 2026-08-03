using MBXDist.Core.Versioning;

namespace MBXDist.Core.Tests.Versioning;

public class PackageVersionTests
{
    [Theory]
    [InlineData("0.5.4.5", "0.5.4.6", -1)]
    [InlineData("0.5.4.6", "0.5.4.5", 1)]
    [InlineData("1.2", "1.2.0", 0)]     // missing trailing = 0
    [InlineData("7.1", "7.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    public void CompareTo_orders_component_wise(string a, string b, int expectedSign)
    {
        var cmp = PackageVersion.Parse(a).CompareTo(PackageVersion.Parse(b));
        Assert.Equal(expectedSign, Math.Sign(cmp));
    }

    [Theory]
    [InlineData("2.1", "2.1", true)]
    [InlineData("2.2", "2.1", true)]
    [InlineData("2.0", "2.1", false)]
    public void Satisfies_is_installed_ge_min(string installed, string min, bool expected)
    {
        Assert.Equal(expected, PackageVersion.Parse(installed).Satisfies(PackageVersion.Parse(min)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.x.0")]
    [InlineData("1..2")]
    public void Parse_rejects_non_numeric(string bad)
    {
        Assert.Throws<FormatException>(() => PackageVersion.Parse(bad));
    }

    [Fact]
    public void ToString_returns_raw_input()
    {
        Assert.Equal("0.5.4.5", PackageVersion.Parse("0.5.4.5").ToString());
    }
}
