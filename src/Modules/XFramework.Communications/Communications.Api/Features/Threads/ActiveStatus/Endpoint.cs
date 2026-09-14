using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Threads.ActiveStatus;

public static class SetThreadActiveStatusEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPatch("/api/communications/threads/{threadId:guid}/active-status", Tags = ["Threads"],
        Summary = "Choose whether to share your active status in this thread")]
    public static Task<Result<CmdResponse>> Handle(SetThreadActiveStatusRequest request, IThreadService service, CancellationToken ct) =>
        service.SetThreadActiveStatusAsync(request, ct);
}
