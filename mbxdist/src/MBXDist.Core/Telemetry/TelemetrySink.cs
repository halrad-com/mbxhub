using System.Text.Json;
using MBXDist.Core.Model;

namespace MBXDist.Core.Telemetry;

/// <summary>Best-effort telemetry. Offline-first: if no sink URL is configured or the POST fails,
/// events are dropped silently (optionally queued to a size-capped local file). Never blocks or
/// fails the operation that emitted the event.</summary>
public sealed class TelemetrySink
{
    private const long MaxQueueBytes = 256 * 1024; // cap the offline queue file

    private readonly string? _sinkUrl;
    private readonly HttpClient _http;
    private readonly string? _queuePath;

    public TelemetrySink(string? sinkUrl, string? queuePath = null, HttpClient? http = null)
    {
        _sinkUrl = sinkUrl;
        _queuePath = queuePath;
        _http = http ?? new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>Fire-and-forget emit. Swallows every failure by design.</summary>
    public async Task EmitAsync(TelemetryEvent evt, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(evt, FeedJson.Options);
        if (!string.IsNullOrEmpty(_sinkUrl))
        {
            try
            {
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(_sinkUrl, content, ct);
                if (resp.IsSuccessStatusCode) return;
            }
            catch
            {
                // fall through to queue/drop
            }
        }
        TryQueue(json);
    }

    private void TryQueue(string json)
    {
        if (string.IsNullOrEmpty(_queuePath)) return; // drop
        try
        {
            var dir = Path.GetDirectoryName(_queuePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_queuePath) && new FileInfo(_queuePath).Length > MaxQueueBytes) return; // cap reached: drop
            File.AppendAllText(_queuePath, json.ReplaceLineEndings(" ") + Environment.NewLine);
        }
        catch
        {
            // drop
        }
    }
}
