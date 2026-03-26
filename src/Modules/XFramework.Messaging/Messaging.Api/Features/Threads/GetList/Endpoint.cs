using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.GetList;

public static class GetThreadListEndpoint
{
    [BoltHandler]
    [MapGet("/api/threads", Tags = ["Threads"],
        Summary = "Get a list of threads for a credential",
        Description = "Returns a paginated list of threads where the credential is a member, including member count and last message preview.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<GetThreadListResponse>> Handle(
        GetThreadListRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadListAsync(request, ct);
    }
}
