using System.Text.Json;
using MBXDist.Core.Model;
namespace MBXDist.Core.State;

public sealed class StateStore
{
    private readonly string _path;
    public StateStore(string path) => _path = Path.GetFullPath(path);
    public LocalState Load()
    {
        if (!File.Exists(_path) && !File.Exists(_path + ".bak")) return new LocalState();
        try { return Read(_path); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or FileNotFoundException)
        {
            if (!File.Exists(_path + ".bak")) throw new InvalidDataException("state is unreadable and no backup exists", ex);
            return Read(_path + ".bak");
        }
    }
    private static LocalState Read(string path)
    {
        var state = JsonSerializer.Deserialize<LocalState>(File.ReadAllText(path), FeedJson.Options)
            ?? throw new InvalidDataException("state is null");
        if (state.SchemaVersion != 1 || state.Packages is null || state.TargetRoots is null || state.InProgress is null)
            throw new InvalidDataException("unsupported or malformed state");
        return state;
    }
    public IDisposable AcquireLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        try { return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("another MBXDist operation is using this config", ex); }
    }
    public void Save(LocalState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(file, state, FeedJson.Options); file.Flush(true); }
            if (File.Exists(_path))
            {
                bool valid = true;
                try { Read(_path); } catch (Exception ex) when (ex is JsonException or InvalidDataException) { valid = false; }
                File.Replace(temp, _path, valid ? _path + ".bak" : null);
            }
            else File.Move(temp, _path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static string EnsureClientId(LocalState state)
    {
        if (string.IsNullOrWhiteSpace(state.ClientId)) state.ClientId = Guid.NewGuid().ToString("D");
        return state.ClientId;
    }
}
