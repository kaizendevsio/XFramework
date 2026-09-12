using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.DeleteThread;

public static class DeleteThreadEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapDelete("/api/communications/threads/{threadId:guid}", Tags = ["Threads"], Summary = "Delete a conversation for everyone")]
    public static Task<Result<CmdResponse>> Handle(DeleteThreadRequest request, IThreadService service, CancellationToken ct) =>
        service.DeleteThreadAsync(request, ct);
}
