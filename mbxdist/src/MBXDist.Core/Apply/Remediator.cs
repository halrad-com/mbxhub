using MBXDist.Core.Integrity;
using MBXDist.Core.Model;

namespace MBXDist.Core.Apply;

public enum DamageKind
{
    Missing,
    HashMismatch
}

public sealed record DamagedResource(ResourceState Resource, DamageKind Kind);

/// <summary>Finds installed resources that are missing or whose on-disk hash no longer matches the recorded hash.</summary>
public sealed class Remediator
{
    public IReadOnlyList<DamagedResource> FindDamaged(PackageState package)
    {
        var damaged = new List<DamagedResource>();
        foreach (var r in package.Resources)
        {
            if (!File.Exists(r.Target))
            {
                damaged.Add(new DamagedResource(r, DamageKind.Missing));
                continue;
            }
            if (!Sha256.Verify(r.Target, r.Sha256))
                damaged.Add(new DamagedResource(r, DamageKind.HashMismatch));
        }
        return damaged;
    }
}
