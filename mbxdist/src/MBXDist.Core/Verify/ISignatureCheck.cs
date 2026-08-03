namespace MBXDist.Core.Verify;

/// <summary>Seam over the OS Authenticode check so Core stays cross-platform and testable.</summary>
public interface ISignatureCheck
{
    SignatureResult Check(string filePath);
}
