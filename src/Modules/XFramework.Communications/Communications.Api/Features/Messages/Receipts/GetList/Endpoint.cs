using Communications.Domain.Shared.Contracts.Requests.Receipts;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Receipts.GetList;

public static class GetMessageReceiptsEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredActorCapabilities = ["communications.chat:view"])]
    [MapGet("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/receipts",
        Tags = ["Messages"], Capability = "view", Summary = "When each member received and read the caller's own message")]
    public static Task<Result<GetMessageReceiptsResponse>> Handle(
        GetMessageReceiptsRequest request, IThreadService service, CancellationToken ct) =>
        service.GetMessageReceiptsAsync(request, ct);
}
