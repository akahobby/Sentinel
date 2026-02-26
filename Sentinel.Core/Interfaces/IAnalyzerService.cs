using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IAnalyzerService
{
    Task<IReadOnlyList<AnalyzerFinding>> AnalyzeAsync(CancellationToken cancellationToken = default);
}
