using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Connections.Create;

public static class CreateConnectionEndpoint
{
    [BoltHandler]
    [MapPost("/api/community/connections", Tags = ["Community Connections"],
        Summary = "Create a new connection",
        Description = "Creates a new connection (follow/block) between two community identities")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateConnectionRequest request,
        IConnectionService connectionService,
        CancellationToken ct)
    {
        return await connectionService.CreateConnectionAsync(request, ct);
    }
}
