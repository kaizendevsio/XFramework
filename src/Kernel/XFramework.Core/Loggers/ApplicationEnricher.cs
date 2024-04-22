using System.Runtime.Versioning;

namespace XFramework.Core.Loggers;

public class ApplicationEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationName", Assembly.GetEntryAssembly()?.GetName().Name?.Split(".")[0]));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ApplicationInstanceId", Guid.NewGuid().ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SystemTime", DateTime.Now)); // Consider using UTC to avoid timezone issues
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MachineName", Environment.MachineName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OSVersion", Environment.OSVersion.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryFootprint", Environment.WorkingSet.Bytes()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Timezone", TimeZoneInfo.Local.StandardName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Version", Assembly.GetEntryAssembly()?.GetName().Version?.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RuntimeVersion", Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName));
    }
}