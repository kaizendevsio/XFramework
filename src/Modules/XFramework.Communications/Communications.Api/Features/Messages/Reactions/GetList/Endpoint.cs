using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Reactions.GetList;

public static class GetMessageReactionsEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredActorCapabilities = ["communications.chat:view"])]
    [MapGet("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/reactions",
        Tags = ["Messages"], Summary = "List reactions visible to a thread member")]
    public static Task<Result<PaginatedResult<MessageReactionResponse>>> Handle(
        GetMessageReactionsRequest request, IThreadService service, CancellationToken ct) =>
        service.GetMessageReactionsAsync(request, ct);
}
