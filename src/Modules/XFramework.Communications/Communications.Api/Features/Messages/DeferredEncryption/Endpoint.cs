using Communications.Domain.Shared.Contracts.Responses;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.DeferredEncryption;

public static class DeferredEncryptionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    public static Task<Result<DeferredEncryptionResponse>> Get(GetDeferredEncryptionRequest request, IThreadService service, CancellationToken ct) =>
        service.GetDeferredEncryptionAsync(request, ct);

    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    public static Task<Result<CmdResponse>> Complete(CompleteDeferredEncryptionRequest request, IThreadService service, CancellationToken ct) =>
        service.CompleteDeferredEncryptionAsync(request, ct);
}
