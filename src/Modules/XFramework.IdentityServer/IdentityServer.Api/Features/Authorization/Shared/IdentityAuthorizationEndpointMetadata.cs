namespace IdentityServer.Api.Features.Authorization.Shared;

public static class IdentityAuthorizationEndpointMetadata
{
    public static void ApplyHttpDiagnostics(RequestMetadata metadata, HttpContext httpContext)
    {
        metadata.IpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        metadata.UserAgent = httpContext.Request.Headers.UserAgent.ToString();
    }
}
