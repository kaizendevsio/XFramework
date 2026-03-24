using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bolt.Server;

public static class BoltMiddleware
{
    /// <summary>
    /// Map the Bolt WebSocket endpoint on the given path.
    /// Usage: app.UseWebSockets(); app.MapBolt("/bolt");
    /// </summary>
    public static IEndpointRouteBuilder MapBolt(this IEndpointRouteBuilder endpoints, string path = "/bolt")
    {
        endpoints.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connections only");
                return;
            }

            var server = context.RequestServices.GetRequiredService<BoltServer>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(webSocket, context.RequestAborted);
        });

        return endpoints;
    }
}
