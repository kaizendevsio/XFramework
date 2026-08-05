using Communications.Domain.Shared.Contracts.Requests.Realtime;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Realtime;

public static class PublishCommunicationsTypingEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/realtime/typing", Tags = ["Communications Realtime"],
        Summary = "Publish typing state",
        Description = "Publishes a server-stamped typing state for the authenticated requester.")]
    public static Task<Result<CmdResponse>> Handle(
        PublishCommunicationsTypingRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PublishTypingAsync(request, ct);
}

public static class PublishCommunicationsPresenceEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/realtime/presence", Tags = ["Communications Realtime"],
        Summary = "Publish presence state",
        Description = "Publishes a server-stamped presence state for the authenticated requester.")]
    public static Task<Result<CmdResponse>> Handle(
        PublishCommunicationsPresenceRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PublishPresenceAsync(request, ct);
}
