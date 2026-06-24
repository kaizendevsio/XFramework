using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.CreateMessage;

public static class CreateThreadMessageEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads/{threadId:guid}/messages", Tags = ["Messages"],
        Summary = "Create a message in a thread",
        Description = "Creates a new message in the specified thread. Validates that the sender is a member of the thread.")]
    public static async Task<Result<CreateThreadMessageResponse>> Handle(
        CreateThreadMessageRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.CreateThreadMessageAsync(request, ct);
    }
}
