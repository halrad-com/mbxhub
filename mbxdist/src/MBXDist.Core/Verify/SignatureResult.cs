namespace MBXDist.Core.Verify;

/// <summary>Outcome of an OS signature check: whether the file's signature is trusted, and the signer cert thumbprint (if any).</summary>
public readonly record struct SignatureResult(bool Trusted, string? Thumbprint);
