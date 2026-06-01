using Messaging.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.CreateVerification;

public static class CreateVerificationMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/messages/verification", Tags = ["Messages"],
        Summary = "Create and send a verification message",
        Description = "Sends a verification token to the requested contact. Phone contacts are sent by SMS.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateVerificationMessageRequest request,
        IMessagingService messagingService,
        CancellationToken ct)
    {
        return await messagingService.CreateVerificationMessageAsync(request, ct);
    }
}
