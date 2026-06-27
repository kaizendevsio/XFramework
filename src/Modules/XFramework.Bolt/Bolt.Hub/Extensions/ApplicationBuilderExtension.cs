using Bolt.Domain.Shared.Contracts.ServiceDiscovery;
using Bolt.Hub.Services;
using Bolt.Server;

namespace Bolt.Hub.Extensions;

public static class ApplicationBuilderExtension
{
    public static IApplicationBuilder UseAppServices(this IApplicationBuilder appBuilder)
    {
        var app = appBuilder as WebApplication
            ?? throw new InvalidOperationException("Bolt Hub service mapping requires WebApplication.");

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Bolt thin-protocol WebSocket endpoint
        app.UseWebSockets();
        app.MapBolt("/bolt/ws").RequireAuthorization();

        if (app.Configuration.GetValue<bool>("BoltServiceDiscovery:ExposeHttpEndpoints"))
        {
            app.MapGet("/api/bolt/services", async (
                    IBoltServiceDiscoveryRegistry registry,
                    CancellationToken ct) =>
                Results.Ok(await registry.GetServicesAsync(new BoltServiceRegistryRequest(), ct)));

            app.MapGet("/api/bolt/modules", async (
                    IBoltServiceDiscoveryRegistry registry,
                    CancellationToken ct) =>
                Results.Ok(await registry.GetModulesAsync(new BoltModuleRegistryRequest(), ct)));
        }

        return app;
    }
}
