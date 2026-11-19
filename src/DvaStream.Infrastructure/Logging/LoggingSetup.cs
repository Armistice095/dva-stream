using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DvaStream.Infrastructure.Logging;

/// <summary>Configuration de Serilog : fichiers tournants dans le dossier des journaux (TDD §13).</summary>
public static class LoggingSetup
{
    public const string LogFilePrefix = "dvastream-";
    public const int RetainedFileCount = 14;

    public static Logger CreateLogger(string logsDirectory, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.WithProperty("App", "DvaStream")
            .WriteTo.File(
                path: Path.Combine(logsDirectory, LogFilePrefix + ".log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
