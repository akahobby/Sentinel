namespace Sentinel.Core.Logging;

using Serilog;
using Serilog.Sinks.File;

public static class AppLog
{
    private static Serilog.ILogger? _logger;

    public static Serilog.ILogger Logger => _logger ??= CreateFallback();

    public static void Init(Serilog.ILogger logger) => _logger = logger;

    private static Serilog.ILogger CreateFallback()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "logs");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
        return new Serilog.LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(path).CreateLogger();
    }

    public static void Debug(string messageTemplate, params object[] args) => Logger.Debug(messageTemplate, args);
    public static void Info(string messageTemplate, params object[] args) => Logger.Information(messageTemplate, args);
    public static void Warn(string messageTemplate, params object[] args) => Logger.Warning(messageTemplate, args);
    public static void Warn(Exception ex, string messageTemplate, params object[] args) => Logger.Warning(ex, messageTemplate, args);
    public static void Error(string messageTemplate, params object[] args) => Logger.Error(messageTemplate, args);
    public static void Error(Exception ex, string messageTemplate, params object[] args) => Logger.Error(ex, messageTemplate, args);
    public static void Fatal(Exception ex, string messageTemplate, params object[] args) => Logger.Fatal(ex, messageTemplate, args);
}
