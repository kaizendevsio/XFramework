using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.MarkRead;

public static class MarkMessagesReadEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads/{threadId:guid}/messages/read", Tags = ["Messages"],
        Summary = "Mark messages as read",
        Description = "Marks the specified messages as read for the requesting member. Creates delivery records if they don't exist.")]
    public static async Task<Result<CmdResponse>> Handle(
        MarkMessagesReadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.MarkMessagesReadAsync(request, ct);
    }
}
