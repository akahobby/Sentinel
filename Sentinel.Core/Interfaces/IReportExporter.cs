using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

public interface IReportExporter
{
    Task WriteLatestJsonAsync(LatestReport report, CancellationToken cancellationToken = default);
    Task<string> ExportZipAsync(CancellationToken cancellationToken = default);
    string GetLogsFolderPath();
    string GetReportsFolderPath();
    string GetExportsFolderPath();
}
