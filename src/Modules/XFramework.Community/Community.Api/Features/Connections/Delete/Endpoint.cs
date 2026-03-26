using Community.Api.Services;
using Community.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Attributes;

namespace Community.Api.Features.Connections.Delete;

public static class DeleteConnectionEndpoint
{
    [BoltHandler]
    [MapDelete("/api/community/connections/{id:guid}", Tags = ["Community Connections"],
        Summary = "Delete a connection",
        Description = "Soft-deletes a connection by ID. The requester must own the connection.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteConnectionRequest request,
        IConnectionService connectionService,
        CancellationToken ct)
    {
        return await connectionService.DeleteConnectionAsync(request, ct);
    }
}
