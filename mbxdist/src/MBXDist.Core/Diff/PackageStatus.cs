namespace MBXDist.Core.Diff;

public enum PackageStatus
{
    UpToDate,
    UpdateAvailable,
    Ahead,     // installed newer than recommended — informational, never auto-downgrade
    Missing
}

public sealed record PackageDiff(
    string Id,
    PackageStatus Status,
    string? RecommendedVersion,
    string? InstalledVersion);
