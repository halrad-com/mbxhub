using System.Net;
using MBXDist.Core.Telemetry;

namespace MBXDist.Core.Tests.Telemetry;

public class TelemetrySinkTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-tel-" + Guid.NewGuid().ToString("N"));

    private string QueuePath => Path.Combine(_dir, "queue.jsonl");

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Posted { get; } = new();
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool Throw { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw) throw new HttpRequestException("sink unreachable");
            Posted.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status);
        }
    }

    private static TelemetryEvent Evt() => new()
    {
        ClientId = "cid", Timestamp = "t", Event = "check", Result = "ok"
    };

    [Fact]
    public async Task Posts_to_sink_when_reachable()
    {
        var handler = new CaptureHandler();
        var sink = new TelemetrySink("http://sink.test/events", QueuePath, new HttpClient(handler));
        await sink.EmitAsync(Evt());
        Assert.Single(handler.Posted);
        Assert.Contains("\"event\": \"check\"", handler.Posted[0]);
        Assert.False(File.Exists(QueuePath)); // no queue on success
    }

    [Fact]
    public async Task Queues_locally_when_sink_unreachable()
    {
        var handler = new CaptureHandler { Throw = true };
        var sink = new TelemetrySink("http://sink.test/events", QueuePath, new HttpClient(handler));
        await sink.EmitAsync(Evt());
        Assert.True(File.Exists(QueuePath));
        Assert.Contains("check", File.ReadAllText(QueuePath));
    }

    [Fact]
    public async Task No_sink_configured_drops_to_queue_without_error()
    {
        var sink = new TelemetrySink(null, QueuePath);
        await sink.EmitAsync(Evt()); // must not throw
        Assert.True(File.Exists(QueuePath));
    }

    [Fact]
    public async Task No_sink_and_no_queue_is_a_silent_drop()
    {
        var sink = new TelemetrySink(null, null);
        await sink.EmitAsync(Evt()); // must not throw
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
