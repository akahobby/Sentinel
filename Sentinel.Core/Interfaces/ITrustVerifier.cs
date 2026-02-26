using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public sealed class SignatureInfo
{
    public bool IsSigned { get; set; }
    public bool IsValid { get; set; }
    public string? Publisher { get; set; }
}

public interface ITrustVerifier
{
    Task<SignatureInfo> GetSignatureInfoAsync(string filePath, CancellationToken cancellationToken = default);
    Task<string?> GetSha256Async(string filePath, CancellationToken cancellationToken = default);
    RiskLevel AssessRisk(string? path, string? publisher, bool isSigned, string? name);
}
