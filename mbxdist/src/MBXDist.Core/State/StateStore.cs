using System.Text.Json;
using MBXDist.Core.Model;

namespace MBXDist.Core.State;

/// <summary>Reads/writes mbxdist-state.json. The client id is generated once and persisted.</summary>
public sealed class StateStore
{
    private readonly string _path;

    public StateStore(string path) => _path = path;

    public LocalState Load()
    {
        if (!File.Exists(_path)) return new LocalState();
        var text = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<LocalState>(text, FeedJson.Options) ?? new LocalState();
    }

    public void Save(LocalState state)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(state, FeedJson.Options));
    }

    /// <summary>Ensure a stable per-machine client id exists (generate-once, persist). SLPhantomRemote installation-ID pattern.</summary>
    public static string EnsureClientId(LocalState state)
    {
        if (string.IsNullOrWhiteSpace(state.ClientId))
            state.ClientId = Guid.NewGuid().ToString("D");
        return state.ClientId;
    }
}
