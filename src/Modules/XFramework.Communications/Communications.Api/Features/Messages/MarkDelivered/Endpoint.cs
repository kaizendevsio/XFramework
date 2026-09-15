using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.MarkDelivered;

public static class MarkMessagesDeliveredEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/delivered", Tags = ["Messages"],
        Summary = "Acknowledge received messages",
        Description = "Records recipient delivery after client application, without changing read receipts.")]
    public static Task<Result<CmdResponse>> Handle(
        MarkMessagesDeliveredRequest request, IThreadService threadService, CancellationToken ct) =>
        threadService.MarkMessagesDeliveredAsync(request, ct);
}
