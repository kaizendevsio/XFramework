using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.Members.Add;

public static class AddThreadMemberEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads/{threadId:guid}/members", Tags = ["Thread Members"],
        Summary = "Add a member to a thread",
        Description = "Adds a credential as a member of the specified thread. Validates that the thread and credential exist, and that the credential is not already a member.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CmdResponse>> Handle(
        AddThreadMemberRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.AddThreadMemberAsync(request, ct);
    }
}
