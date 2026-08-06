using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.Create;

public static class CreateThreadEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/threads", Tags = ["Threads"],
        Summary = "Create a new message thread",
        Description = "Creates a new message thread with initial members. The first member is considered the creator.")]
    public static async Task<Result<CreateThreadResponse>> Handle(
        CreateThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.CreateThreadAsync(request, ct);
    }
}
