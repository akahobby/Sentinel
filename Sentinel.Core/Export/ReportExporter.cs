using System.IO.Compression;
using System.Text.Json;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Core.Export;

public sealed class ReportExporter : IReportExporter
{
    private readonly string _baseFolder;

    public ReportExporter()
    {
        _baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel");
    }

    public string GetLogsFolderPath() => Path.Combine(_baseFolder, "logs");
    public string GetReportsFolderPath() => Path.Combine(_baseFolder, "reports");
    public string GetExportsFolderPath() => Path.Combine(_baseFolder, "exports");

    public async Task WriteLatestJsonAsync(LatestReport report, CancellationToken cancellationToken = default)
    {
        var dir = GetReportsFolderPath();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "latest.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExportZipAsync(CancellationToken cancellationToken = default)
    {
        var exportsDir = GetExportsFolderPath();
        Directory.CreateDirectory(exportsDir);
        var name = $"Sentinel_Report_{DateTime.UtcNow:yyyy-MM-dd_HH-mm}.zip";
        var path = Path.Combine(exportsDir, name);

        await Task.Run(() =>
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            var reportsDir = GetReportsFolderPath();
            if (File.Exists(Path.Combine(reportsDir, "latest.json")))
                zip.CreateEntryFromFile(Path.Combine(reportsDir, "latest.json"), "latest.json");
            var logsDir = GetLogsFolderPath();
            if (Directory.Exists(logsDir))
            {
                foreach (var f in Directory.GetFiles(logsDir, "*.log"))
                    zip.CreateEntryFromFile(f, "logs/" + Path.GetFileName(f));
            }
        }, cancellationToken).ConfigureAwait(false);

        return path;
    }
}
