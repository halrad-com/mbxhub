namespace MBXDist.Core.Net;

/// <summary>Fetches feed documents/binaries relative to a base URL.</summary>
public interface IFeedClient
{
    Task<string> GetTextAsync(string relativeUrl, CancellationToken ct = default);
    Task DownloadToAsync(string relativeUrl, string destPath, CancellationToken ct = default);
}
