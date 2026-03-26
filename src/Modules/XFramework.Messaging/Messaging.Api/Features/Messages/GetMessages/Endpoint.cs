using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.GetMessages;

public static class GetThreadMessagesEndpoint
{
    [BoltHandler]
    [MapGet("/api/threads/{threadId:guid}/messages", Tags = ["Messages"],
        Summary = "Get messages for a thread",
        Description = "Returns a paginated list of messages for the specified thread, ordered by creation date descending. Validates that the requester is a member.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<GetThreadMessagesResponse>> Handle(
        GetThreadMessagesRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadMessagesAsync(request, ct);
    }
}
