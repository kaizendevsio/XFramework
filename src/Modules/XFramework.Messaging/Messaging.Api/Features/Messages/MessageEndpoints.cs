using Messaging.Api.Features.Messages.CreateDirect;
using Messaging.Api.Features.Messages.UpdateDirect;

namespace Messaging.Api.Features.Messages;

/// <summary>
/// Extension methods for registering Message endpoints
/// </summary>
public static class MessageEndpoints
{
    /// <summary>
    /// Maps all Message endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Messages")
            .WithOpenApi();

        // Map individual endpoints
        app.MapCreateDirectMessage();
        app.MapUpdateDirectMessage();

        return app;
    }
}