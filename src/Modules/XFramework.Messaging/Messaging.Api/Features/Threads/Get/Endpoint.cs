using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.Get;

public static class GetThreadEndpoint
{
    [BoltHandler]
    [MapGet("/api/threads/{id:guid}", Tags = ["Threads"],
        Summary = "Get a thread by ID",
        Description = "Returns a thread with its members list when the requester is a member.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<GetThreadResponse>> Handle(
        GetThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadAsync(request, ct);
    }
}
