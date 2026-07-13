using Bolt.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace XFramework.Integration.Health;

public sealed class BoltClientTransportHealthCheck(BoltClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = client.GetHealthSnapshot();
        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
        {
            ["transport"] = snapshot
        };

        return Task.FromResult(snapshot.IsHealthy
            ? HealthCheckResult.Healthy("Bolt client transport and loops are healthy.", data)
            : HealthCheckResult.Unhealthy("Bolt client transport or loops are unhealthy.", data: data));
    }
}

public static class BoltClientTransportHealthCheckExtensions
{
    public const string RegistrationName = "Bolt-client-transport";

    public static IServiceCollection AddBoltClientTransportHealthCheck(this IServiceCollection services)
    {
        services.TryAddSingleton<BoltClientTransportHealthCheck>();
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            if (options.Registrations.Any(static registration => registration.Name == RegistrationName))
                return;

            options.Registrations.Add(new HealthCheckRegistration(
                RegistrationName,
                serviceProvider => serviceProvider.GetRequiredService<BoltClientTransportHealthCheck>(),
                HealthStatus.Unhealthy,
                ["bolt", "transport", "client", "ready"],
                timeout: null));
        });

        return services;
    }
}
