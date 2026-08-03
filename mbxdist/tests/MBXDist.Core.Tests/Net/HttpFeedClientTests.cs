using System.Net;
using MBXDist.Core.Net;

namespace MBXDist.Core.Tests.Net;

public class HttpFeedClientTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mbxdist-feed-" + Guid.NewGuid().ToString("N"));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode, string)> _map;
        public StubHandler(Dictionary<string, (HttpStatusCode, string)> map) => _map = map;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri!.AbsoluteUri;
            if (_map.TryGetValue(key, out var v))
                return Task.FromResult(new HttpResponseMessage(v.Item1) { Content = new StringContent(v.Item2) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static HttpFeedClient Client(Dictionary<string, (HttpStatusCode, string)> map)
        => new("http://feed.test/base", new HttpClient(new StubHandler(map)));

    [Fact]
    public async Task GetTextAsync_resolves_relative_and_trims()
    {
        var c = Client(new() { ["http://feed.test/base/latest.txt"] = (HttpStatusCode.OK, "0.5.4.6\n") });
        Assert.Equal("0.5.4.6", await c.GetTextAsync("latest.txt"));
    }

    [Fact]
    public async Task DownloadToAsync_writes_body_to_file()
    {
        var dest = Path.Combine(_dir, "sub", "mb.dll");
        var c = Client(new() { ["http://feed.test/base/core/0.5.4.6/mb.dll"] = (HttpStatusCode.OK, "BINARYBYTES") });
        await c.DownloadToAsync("core/0.5.4.6/mb.dll", dest);
        Assert.Equal("BINARYBYTES", await File.ReadAllTextAsync(dest));
    }

    [Fact]
    public async Task GetTextAsync_throws_on_404()
    {
        var c = Client(new());
        await Assert.ThrowsAsync<HttpRequestException>(() => c.GetTextAsync("missing.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
