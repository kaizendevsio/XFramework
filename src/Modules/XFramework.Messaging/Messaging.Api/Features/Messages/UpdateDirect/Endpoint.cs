using Messaging.Domain.Shared.Contracts.Requests.Update;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.UpdateDirect;

public static class UpdateDirectMessageEndpoint
{
    [BoltHandler]
    [MapPatch("/api/messages/direct", Tags = ["Messages"],
        Summary = "Update a direct message")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateMessageDirectRequest request,
        IMessagingService messagingService,
        CancellationToken ct)
    {
        return await messagingService.UpdateMessageDirectAsync(request, ct);
    }
}
