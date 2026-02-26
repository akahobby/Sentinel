using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Trust;

public sealed class TrustVerifier : ITrustVerifier
{
    private static readonly string[] SuspiciousPaths = { "\\temp\\", "\\tmp\\", "\\appdata\\local\\temp\\", "\\users\\" };
    private static readonly string[] LookAlikePatterns = { "svch0st", "svchost", "exp1orer", "explorer", "csrss", "csrsss", "lsass", "1sass" };

    public Task<SignatureInfo> GetSignatureInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Task.FromResult(new SignatureInfo { IsSigned = false, IsValid = false });

        return Task.Run(() =>
        {
            try
            {
                using var cert = X509Certificate.CreateFromSignedFile(filePath);
                if (cert == null || string.IsNullOrEmpty(cert.Subject))
                    return new SignatureInfo { IsSigned = false, IsValid = false };
                var publisher = cert.Subject;
                var cn = GetCnFromSubject(cert.Subject);
                return new SignatureInfo
                {
                    IsSigned = true,
                    IsValid = true,
                    Publisher = cn ?? publisher
                };
            }
            catch (CryptographicException)
            {
                return new SignatureInfo { IsSigned = false, IsValid = false };
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return new SignatureInfo { IsSigned = false, IsValid = false };
            }
        }, cancellationToken);
    }

    public Task<string?> GetSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return Task.FromResult<string?>(null);
        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var hash = SHA256.HashData(stream);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch
            {
                return (string?)null;
            }
        }, cancellationToken);
    }

    public RiskLevel AssessRisk(string? path, string? publisher, bool isSigned, string? name)
    {
        if (string.IsNullOrEmpty(path))
            return RiskLevel.Unknown;
        var pathLower = path.ToLowerInvariant();
        var nameLower = (name ?? Path.GetFileName(path)).ToLowerInvariant();

        if (LookAlikePatterns.Any(p => nameLower.Contains(p.Replace("1", "l")) || nameLower.Contains(p.Replace("0", "o"))))
            return RiskLevel.Suspicious;
        if (!isSigned && SuspiciousPaths.Any(p => pathLower.Contains(p)))
            return RiskLevel.High;
        if (!isSigned)
            return RiskLevel.Medium;
        if (string.IsNullOrEmpty(publisher))
            return RiskLevel.Low;
        return RiskLevel.Ok;
    }

    private static string? GetCnFromSubject(string subject)
    {
        var cn = "CN=";
        var i = subject.IndexOf(cn, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += cn.Length;
        var end = subject.IndexOf(',', i);
        return end < 0 ? subject[i..].Trim() : subject[i..end].Trim();
    }
}
