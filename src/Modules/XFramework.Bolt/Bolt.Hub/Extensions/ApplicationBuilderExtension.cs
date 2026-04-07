using Bolt.Server;

namespace Bolt.Hub.Extensions;

public static class ApplicationBuilderExtension
{
    public static IApplicationBuilder UseAppServices(this IApplicationBuilder appBuilder)
    {
        var app = appBuilder as WebApplication;

        // Bolt thin-protocol WebSocket endpoint
        app.UseWebSockets();
        app.MapBolt("/bolt/ws");

        return app as IApplicationBuilder;
    }
}