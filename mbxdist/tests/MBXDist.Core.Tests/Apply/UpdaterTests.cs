using MBXDist.Core.Apply;

namespace MBXDist.Core.Tests.Apply;

public class UpdaterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-upd-" + Guid.NewGuid().ToString("N"));

    private string P(string name)
    {
        Directory.CreateDirectory(_dir);
        return Path.Combine(_dir, name);
    }

    [Fact]
    public void ApplyStaged_replaces_free_target_and_reports_Applied()
    {
        var staged = P("staged.bin");
        var target = P("target.bin");
        File.WriteAllText(target, "old");
        File.WriteAllText(staged, "new");

        var result = new Updater().ApplyStaged(staged, target);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal("new", File.ReadAllText(target));
        Assert.False(File.Exists(staged));
    }

    [Fact]
    public void ApplyStaged_stages_pending_when_target_locked_and_leaves_target_untouched()
    {
        var staged = P("staged.bin");
        var target = P("locked.bin");
        File.WriteAllText(target, "live");
        File.WriteAllText(staged, "new");

        using (var held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = new Updater().ApplyStaged(staged, target);

            Assert.Equal(ApplyOutcome.StagedPending, result.Outcome);
            Assert.Equal("new", File.ReadAllText(target + ".pending"));
            Assert.Equal("live", File.ReadAllText(target)); // untouched
            Assert.False(File.Exists(staged));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
