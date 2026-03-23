using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace StreamFlow.Stream.ThinProtocol;

public static class ThinStreamFlowMiddleware
{
    /// <summary>
    /// Map the thin StreamFlow WebSocket endpoint.
    /// Runs alongside the existing SignalR hub for migration.
    /// </summary>
    public static IEndpointRouteBuilder MapThinStreamFlow(this IEndpointRouteBuilder endpoints, string path = "/streamflow/ws")
    {
        endpoints.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connections only");
                return;
            }

            var server = context.RequestServices.GetRequiredService<ThinStreamFlowServer>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(webSocket, context.RequestAborted);
        });

        return endpoints;
    }
}
