using Serilog;
using Serilog.Events;

namespace Sentinel.Core.Logging;

public static class LoggingSetup
{
    public static void Initialize()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "logs");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .Enrich.WithProperty("Module", "Sentinel")
            .CreateLogger();
        AppLog.Init(logger);
    }
}
