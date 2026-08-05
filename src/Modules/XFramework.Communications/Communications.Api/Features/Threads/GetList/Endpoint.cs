using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.GetList;

public static class GetThreadListEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapGet("/api/communications/threads", Tags = ["Threads"],
        Summary = "Get a list of threads for a credential",
        Description = "Returns a paginated list of threads where the credential is a member, including member count and last message preview.")]
    public static async Task<Result<GetThreadListResponse>> Handle(
        GetThreadListRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetThreadListAsync(request, ct);
    }
}
