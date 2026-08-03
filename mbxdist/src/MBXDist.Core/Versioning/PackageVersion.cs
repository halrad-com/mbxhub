using System.Globalization;

namespace MBXDist.Core.Versioning;

/// <summary>Dotted numeric version (MAJOR.MINOR.PATCH[.BUILD]); missing trailing components compare as 0.</summary>
public sealed class PackageVersion : IComparable<PackageVersion>
{
    private readonly int[] _parts;
    public string Raw { get; }

    private PackageVersion(int[] parts, string raw) { _parts = parts; Raw = raw; }

    public static PackageVersion Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new FormatException("version is empty");
        var segs = s.Split('.');
        var parts = new int[segs.Length];
        for (int i = 0; i < segs.Length; i++)
        {
            if (!int.TryParse(segs[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                throw new FormatException($"non-numeric version segment '{segs[i]}' in '{s}'");
            parts[i] = n;
        }
        return new PackageVersion(parts, s);
    }

    public int CompareTo(PackageVersion? other)
    {
        if (other is null) return 1;
        int len = Math.Max(_parts.Length, other._parts.Length);
        for (int i = 0; i < len; i++)
        {
            int a = i < _parts.Length ? _parts[i] : 0;
            int b = i < other._parts.Length ? other._parts[i] : 0;
            if (a != b) return a.CompareTo(b);
        }
        return 0;
    }

    public bool Satisfies(PackageVersion min) => CompareTo(min) >= 0;

    public override string ToString() => Raw;
}
