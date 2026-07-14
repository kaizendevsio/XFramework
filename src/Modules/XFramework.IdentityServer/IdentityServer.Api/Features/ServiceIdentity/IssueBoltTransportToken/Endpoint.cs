using Microsoft.AspNetCore.Http;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;

public static class IssueBoltTransportTokenEndpoint
{
    [MapPost("/api/service-identity/bolt-transport-token", Tags = ["Service Identity"],
        Summary = "Issue Bolt transport token",
        Description = "Issues a short-lived Bolt transport token for the authenticated service client.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<ServiceTokenResponse>> Handle(
        IssueBoltTransportTokenRequest request,
        HttpRequest httpRequest,
        ServiceIdentityConfiguration configuration,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        if (!configuration.AllowInsecureHttp && !httpRequest.IsHttps)
            return Result<ServiceTokenResponse>.Failure("HTTPS is required", 400);

        return await serviceIdentityService.IssueBoltTransportTokenAsync(
            request.ClientId,
            request.ClientSecret,
            ct);
    }
}

public sealed record IssueBoltTransportTokenRequest
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}
