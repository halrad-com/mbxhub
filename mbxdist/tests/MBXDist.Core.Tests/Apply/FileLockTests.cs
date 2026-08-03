using MBXDist.Core.Apply;

namespace MBXDist.Core.Tests.Apply;

public class FileLockTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-lock-" + Guid.NewGuid().ToString("N"));

    private string P(string name)
    {
        Directory.CreateDirectory(_dir);
        return Path.Combine(_dir, name);
    }

    [Fact]
    public void IsLocked_false_for_missing_file()
    {
        Assert.False(FileLock.IsLocked(P("nope.bin")));
    }

    [Fact]
    public void IsLocked_false_for_free_file()
    {
        var f = P("free.bin");
        File.WriteAllText(f, "x");
        Assert.False(FileLock.IsLocked(f));
    }

    [Fact]
    public void IsLocked_true_while_held_exclusively()
    {
        var f = P("held.bin");
        File.WriteAllText(f, "x");
        using var held = new FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(FileLock.IsLocked(f));
    }

    [Fact]
    public void AtomicReplace_moves_when_target_absent()
    {
        var staged = P("staged1.bin");
        var target = P("target1.bin");
        File.WriteAllText(staged, "new");
        FileLock.AtomicReplace(staged, target);
        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void AtomicReplace_overwrites_when_target_present()
    {
        var staged = P("staged2.bin");
        var target = P("target2.bin");
        File.WriteAllText(target, "old");
        File.WriteAllText(staged, "new");
        FileLock.AtomicReplace(staged, target);
        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(staged));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
