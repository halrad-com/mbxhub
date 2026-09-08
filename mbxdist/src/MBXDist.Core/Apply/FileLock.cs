namespace MBXDist.Core.Apply;

/// <summary>File lock detection and atomic replacement of an install target.</summary>
public static class FileLock
{
    /// <summary>True if the file exists but cannot be opened for exclusive read/write (another process holds it).</summary>
    public static bool IsLocked(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>Replace target with staged. Uses File.Replace for atomicity when the target exists; otherwise moves.</summary>
    public static void AtomicReplace(string stagedPath, string targetPath)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var adjacent = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var source = File.OpenRead(stagedPath))
            using (var destination = new FileStream(adjacent, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { source.CopyTo(destination); destination.Flush(true); }
            if (File.Exists(targetPath)) File.Replace(adjacent, targetPath, null);
            else File.Move(adjacent, targetPath);
            File.Delete(stagedPath);
        }
        finally { if (File.Exists(adjacent)) File.Delete(adjacent); }
    }
}
