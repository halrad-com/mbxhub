namespace MBXDist.Core.Verify;

/// <summary>Outcome of an OS signature check: whether the file's signature is trusted, and the signer
/// cert's <b>SHA-256</b> hash (if any).
///
/// <para><b>The identifier is SHA-256, deliberately.</b> The platform default —
/// <c>X509Certificate2.Thumbprint</c> — is SHA-1, and so is the "SHA1 hash" that <c>signtool verify /v</c>
/// prints. SHA-1 is collision-broken, so pins use the SHA-256 cert hash instead. The property is named
/// for its algorithm precisely so a SHA-1 value from signtool is never pasted into a pin by mistake.</para>
///
/// <para>Verified available on both net8 and net48 (<c>GetCertHashString(HashAlgorithmName.SHA256)</c>),
/// so this costs no dependency and no target-framework compromise.</para></summary>
public readonly record struct SignatureResult(bool Trusted, string? ThumbprintSha256);
