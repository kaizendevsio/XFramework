using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.IssueToken;

public static class IssueServiceTokenEndpoint
{
    [BoltHandler]
    [MapPost("/api/service-identity/token", Tags = ["Service Identity"],
        Summary = "Issue service token",
        Description = "Issues a short-lived internal service token for a target XFramework service.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<ServiceTokenResponse>> Handle(
        IssueServiceTokenRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        return await serviceIdentityService.IssueTokenAsync(request, ct);
    }
}
