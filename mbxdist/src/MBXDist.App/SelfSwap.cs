using System.Diagnostics;
namespace MBXDist.App;

/// <summary>Process handoff for an already verified standalone client. The caller owns signature verification.</summary>
public static class SelfSwap
{
    public static async Task<int> RunAsync(string stagedPath, string ownPath, IEnumerable<string> arguments)
    {
        var next = ownPath + ".new." + Guid.NewGuid().ToString("N");
        var old = ownPath + ".old." + Guid.NewGuid().ToString("N");
        bool moved = false, started = false;
        try
        {
            // Prepare on the installation volume before moving the running executable.
            using (var source = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var dest = new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await source.CopyToAsync(dest); dest.Flush(true); }
            File.Move(ownPath, old); moved = true;
            File.Move(next, ownPath);
            var start = new ProcessStartInfo(ownPath) { UseShellExecute = false };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var child = Process.Start(start) ?? throw new IOException("replacement process could not start");
            started = true;
            await child.WaitForExitAsync();
            return child.ExitCode;
        }
        catch
        {
            if (moved && !started)
            {
                if (File.Exists(ownPath)) File.Delete(ownPath);
                File.Move(old, ownPath);
            }
            throw;
        }
        finally
        {
            TryDelete(next);
            if (started) TryDelete(old); // running parent may hold this until exit; next launch cleans it
        }
    }
    public static void Cleanup(string ownPath)
    {
        var directory = Path.GetDirectoryName(ownPath)!;
        foreach (var path in Directory.EnumerateFiles(directory, Path.GetFileName(ownPath) + ".old.*")) TryDelete(path);
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
