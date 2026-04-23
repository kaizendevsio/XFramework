using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Logging;
using ZLogger;

namespace XFramework.Integration.Extensions;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddXFrameworkLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Debug);

        // Console: plain text, Warning+ baseline
        logging.AddZLoggerConsole(options =>
        {
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[{0} {1}] ", (in MessageTemplate template, in LogInfo info) =>
                    template.Format(info.Timestamp.Local.ToString("HH:mm:ss"), info.LogLevel));
            });
        });

        // Console filters: suppress noise
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>(level => level >= LogLevel.Warning);
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("XFramework.Integration", LogLevel.None);
        logging.AddFilter<ZLogger.Providers.ZLoggerConsoleLoggerProvider>("Bolt", LogLevel.None);

        // Seq: Debug+ for everything (if configured)
        var seqUrl = configuration["Seq:Url"];
        if (!string.IsNullOrEmpty(seqUrl))
        {
            var apiKey = configuration["Seq:ApiKey"];
            var appName = configuration["BoltConfiguration:ClientName"] ?? "Unknown";
            ZLoggerSeqSink.Register(logging, seqUrl, apiKey, LogLevel.Debug, appName);
        }

        return logging;
    }
}
