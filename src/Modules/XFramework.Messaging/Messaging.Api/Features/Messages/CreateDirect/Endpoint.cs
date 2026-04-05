using Messaging.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.CreateDirect;

public static class CreateDirectMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/messages/direct", Tags = ["Messages"],
        Summary = "Create and send a direct message",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        CreateDirectMessageRequest request,
        IMessagingService messagingService,
        CancellationToken ct)
    {
        return await messagingService.CreateDirectMessageAsync(request, ct);
    }
}
