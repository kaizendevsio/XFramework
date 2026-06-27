using Communications.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.CreateVerification;

public static class CreateVerificationMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/messages/verification", Tags = ["Messages"],
        Summary = "Create and send a verification message",
        Description = "Sends a verification token to the requested contact. Phone contacts are sent by SMS.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateVerificationMessageRequest request,
        ICommunicationsService communicationsService,
        CancellationToken ct)
    {
        return await communicationsService.CreateVerificationMessageAsync(request, ct);
    }
}
