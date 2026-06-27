using Communications.Domain.Shared.Contracts.Requests.Update;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.UpdateDirect;

public static class UpdateDirectMessageEndpoint
{
    [BoltHandler]
    [MapPatch("/api/communications/messages/direct", Tags = ["Messages"],
        Summary = "Update a direct message")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateMessageDirectRequest request,
        ICommunicationsService communicationsService,
        CancellationToken ct)
    {
        return await communicationsService.UpdateMessageDirectAsync(request, ct);
    }
}
