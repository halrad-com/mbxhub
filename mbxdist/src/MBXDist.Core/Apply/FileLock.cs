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

        if (File.Exists(targetPath))
            File.Replace(stagedPath, targetPath, destinationBackupFileName: null);
        else
            File.Move(stagedPath, targetPath);
    }
}
