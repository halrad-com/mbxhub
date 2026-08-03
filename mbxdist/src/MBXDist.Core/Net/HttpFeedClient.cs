namespace MBXDist.Core.Net;

/// <summary>HTTP feed client. Resolves relative URLs against a base and streams downloads to disk.</summary>
public sealed class HttpFeedClient : IFeedClient
{
    private readonly HttpClient _http;
    private readonly Uri _base;

    public HttpFeedClient(string baseUrl, HttpClient? http = null)
    {
        _base = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _http = http ?? new HttpClient();
    }

    public async Task<string> GetTextAsync(string relativeUrl, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(new Uri(_base, relativeUrl), ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadAsStringAsync(ct)).Trim();
    }

    public async Task DownloadToAsync(string relativeUrl, string destPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var resp = await _http.GetAsync(new Uri(_base, relativeUrl), HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);
        await src.CopyToAsync(dst, ct);
    }
}
