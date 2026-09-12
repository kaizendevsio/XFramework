using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.GetDeletedThreads;

public static class GetDeletedThreadsEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapGet("/api/communications/threads/deleted", Tags = ["Threads"], Summary = "Get deleted conversation IDs for the caller's offline sync")]
    public static Task<Result<GetDeletedThreadsResponse>> Handle(GetDeletedThreadsRequest request, IThreadService service, CancellationToken ct) =>
        service.GetDeletedThreadsAsync(request, ct);
}
