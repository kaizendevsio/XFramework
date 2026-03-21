using Community.Api.Features.Connections.GetList;

namespace Community.Api.Features.Connections;

/// <summary>
/// Extension methods for registering Connection endpoints
/// </summary>
public static class ConnectionEndpoints
{
    /// <summary>
    /// Maps all Connection endpoints to the application
    /// </summary>
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/community/connections")
            .WithTags("Community Connections")
            .WithOpenApi();

        // Map individual endpoints
        app.MapGetConnectionList();

        return app;
    }
}