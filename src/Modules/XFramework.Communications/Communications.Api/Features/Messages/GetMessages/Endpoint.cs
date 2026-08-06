using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.GetMessages;

public static class GetThreadMessagesEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapGet("/api/communications/threads/{threadId:guid}/messages", Tags = ["Messages"],
        Summary = "Get messages for a thread",
        Description = "Returns a paginated list of messages for the specified thread, ordered by creation date descending. Validates that the requester is a member.")]
    public static async Task<Result<GetThreadMessagesResponse>> Handle(
        GetThreadMessagesRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadMessagesAsync(request, ct);
    }
}
