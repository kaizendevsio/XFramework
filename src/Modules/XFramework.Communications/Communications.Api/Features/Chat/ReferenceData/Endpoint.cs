using Communications.Domain.Shared.Contracts.Requests.ReferenceData;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Chat.ReferenceData;

public static class EnsureChatDefaultsEndpoint
{
    // This exposes only fixed module-owned defaults, never arbitrary reference-data writes.
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredActorCapabilities = ["communications.chat:create"])]
    [MapPost("/api/communications/chat/defaults", Tags = ["Chat"],
        Summary = "Ensure the authenticated tenant has the standard chat reference data")]
    public static Task<Result<ChatReferenceDataResponse>> Handle(
        EnsureChatDefaultsRequest request, ChatReferenceDataService service, CancellationToken ct) =>
        service.EnsureAsync(request, ct);
}

public static class GetChatReferenceDataEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat],
        RequiredActorCapabilities = ["communications.chat:view"])]
    [MapGet("/api/communications/chat/reference-data", Tags = ["Chat"],
        Summary = "Discover the authenticated tenant's chat and reaction types")]
    public static Task<Result<ChatReferenceDataResponse>> Handle(
        GetChatReferenceDataRequest request, ChatReferenceDataService service, CancellationToken ct) =>
        service.GetAsync(request, ct);
}
