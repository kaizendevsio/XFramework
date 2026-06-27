using Communications.Domain.Shared.Contracts.Requests.Create;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.CreateDirect;

public static class CreateDirectMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/messages/direct", Tags = ["Messages"],
        Summary = "Create and send a direct message")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateDirectMessageRequest request,
        ICommunicationsService communicationsService,
        CancellationToken ct)
    {
        return await communicationsService.CreateDirectMessageAsync(request, ct);
    }
}
