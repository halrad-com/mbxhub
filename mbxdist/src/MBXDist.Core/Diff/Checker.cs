using MBXDist.Core.Model;
using MBXDist.Core.Versioning;

namespace MBXDist.Core.Diff;

/// <summary>Diffs the recommended catalog against local install state. Status is measured vs the recommendation, never newest-on-host.</summary>
public sealed class Checker
{
    public IReadOnlyList<PackageDiff> Diff(Catalog catalog, LocalState state)
    {
        var result = new List<PackageDiff>();
        foreach (var entry in catalog.Packages)
        {
            if (!state.Packages.TryGetValue(entry.Id, out var ps) || ps is null)
            {
                result.Add(new PackageDiff(entry.Id, PackageStatus.Missing, entry.Version, null));
                continue;
            }

            int cmp = PackageVersion.Parse(entry.Version).CompareTo(PackageVersion.Parse(ps.Version));
            var status = cmp > 0 ? PackageStatus.UpdateAvailable
                       : cmp < 0 ? PackageStatus.Ahead
                       : PackageStatus.UpToDate;
            result.Add(new PackageDiff(entry.Id, status, entry.Version, ps.Version));
        }
        return result;
    }
}
