namespace MBXDist.Core.Apply;

public enum ApplyOutcome
{
    Applied,
    StagedPending   // target was locked; new file staged as <target>.pending
}

public sealed record ApplyResult(string TargetPath, ApplyOutcome Outcome);

/// <summary>Applies a verified, staged file to its install target. Never overwrites a locked target.</summary>
public sealed class Updater
{
    public ApplyResult ApplyStaged(string stagedPath, string targetPath)
    {
        if (FileLock.IsLocked(targetPath))
        {
            var pending = targetPath + ".pending";
            if (!string.Equals(Path.GetFullPath(stagedPath), Path.GetFullPath(pending), StringComparison.OrdinalIgnoreCase))
                FileLock.AtomicReplace(stagedPath, pending);
            return new ApplyResult(targetPath, ApplyOutcome.StagedPending);
        }

        FileLock.AtomicReplace(stagedPath, targetPath);
        if (File.Exists(targetPath + ".pending")) File.Delete(targetPath + ".pending");
        return new ApplyResult(targetPath, ApplyOutcome.Applied);
    }
}
