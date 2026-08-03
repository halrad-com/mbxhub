namespace MBXDist.Core.Telemetry;

/// <summary>One structured telemetry event per operation. No personal data — clientId is a random GUID.</summary>
public sealed class TelemetryEvent
{
    public string ClientId { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string Event { get; set; } = "";          // check | download | apply | verify | repair | error
    public string? Package { get; set; }
    public string? FromVersion { get; set; }
    public string? ToVersion { get; set; }
    public string Result { get; set; } = "";
    public string? Detail { get; set; }
}
