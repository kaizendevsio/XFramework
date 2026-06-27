using Messaging.Domain.Shared.Contracts.Requests.Realtime;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Realtime;

public static class PublishMessagingTypingEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/realtime/typing", Tags = ["Messaging Realtime"],
        Summary = "Publish typing state",
        Description = "Publishes a server-stamped typing state for the authenticated requester.")]
    public static Task<Result<CmdResponse>> Handle(
        PublishMessagingTypingRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PublishTypingAsync(request, ct);
}

public static class PublishMessagingPresenceEndpoint
{
    [BoltHandler]
    [MapPost("/api/messaging/realtime/presence", Tags = ["Messaging Realtime"],
        Summary = "Publish presence state",
        Description = "Publishes a server-stamped presence state for the authenticated requester.")]
    public static Task<Result<CmdResponse>> Handle(
        PublishMessagingPresenceRequest request,
        IThreadService threadService,
        CancellationToken ct) =>
        threadService.PublishPresenceAsync(request, ct);
}
