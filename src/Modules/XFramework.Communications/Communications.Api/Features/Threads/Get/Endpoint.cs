using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.Get;

public static class GetThreadEndpoint
{
    [BoltHandler]
    [MapGet("/api/communications/threads/{id:guid}", Tags = ["Threads"],
        Summary = "Get a thread by ID",
        Description = "Returns a thread with its members list when the requester is a member.")]
    public static async Task<Result<GetThreadResponse>> Handle(
        GetThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadAsync(request, ct);
    }
}
