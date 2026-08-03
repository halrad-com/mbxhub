using MBXDist.Core.Model;
using MBXDist.Core.State;

namespace MBXDist.Core.Tests.State;

public class StateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-test-" + Guid.NewGuid().ToString("N"));

    private string StatePath => Path.Combine(_dir, "mbxdist-state.json");

    [Fact]
    public void Load_missing_file_returns_default_state()
    {
        var store = new StateStore(StatePath);
        var s = store.Load();
        Assert.NotNull(s);
        Assert.Empty(s.Packages);
        Assert.Equal("", s.ClientId);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new StateStore(StatePath);
        var s = new LocalState { ClientId = "abc" };
        s.TargetRoots["truedat"] = "C:/x";
        store.Save(s);

        var back = new StateStore(StatePath).Load();
        Assert.Equal("abc", back.ClientId);
        Assert.Equal("C:/x", back.TargetRoots["truedat"]);
    }

    [Fact]
    public void EnsureClientId_generates_once_and_persists()
    {
        var s = new LocalState();
        var id1 = StateStore.EnsureClientId(s);
        var id2 = StateStore.EnsureClientId(s);
        Assert.False(string.IsNullOrWhiteSpace(id1));
        Assert.Equal(id1, id2); // stable within the object

        var store = new StateStore(StatePath);
        store.Save(s);
        var reloaded = store.Load();
        Assert.Equal(id1, reloaded.ClientId); // stable across reload
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
