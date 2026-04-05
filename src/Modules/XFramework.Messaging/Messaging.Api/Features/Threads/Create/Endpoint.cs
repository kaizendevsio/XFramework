using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.Create;

public static class CreateThreadEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads", Tags = ["Threads"],
        Summary = "Create a new message thread",
        Description = "Creates a new message thread with initial members. The first member is considered the creator.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<CreateThreadResponse>> Handle(
        CreateThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.CreateThreadAsync(request, ct);
    }
}
