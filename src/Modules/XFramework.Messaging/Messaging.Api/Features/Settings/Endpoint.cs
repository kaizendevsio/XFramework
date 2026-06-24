using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Settings;

public static class GetMessagingSettingsEndpoint
{
    [BoltHandler]
    [MapGet("/api/messaging/settings", Tags = ["Messaging Settings"],
        Summary = "Get Messaging tenant settings",
        Description = "Returns grouped Messaging settings for the authenticated tenant, including stored values and defaults.")]
    public static Task<Result<MessagingSettingsResponse>> Handle(
        GetMessagingSettingsRequest request,
        IMessagingSettingsService settingsService,
        CancellationToken ct) =>
        settingsService.GetSettingsAsync(request, ct);
}

public static class UpdateMessagingSettingsEndpoint
{
    [BoltHandler]
    [MapPut("/api/messaging/settings", Tags = ["Messaging Settings"],
        Summary = "Update Messaging tenant settings",
        Description = "Validates and persists tenant-scoped Messaging settings as RegistryConfiguration rows.")]
    public static Task<Result<MessagingSettingsResponse>> Handle(
        UpdateMessagingSettingsRequest request,
        IMessagingSettingsService settingsService,
        CancellationToken ct) =>
        settingsService.UpdateSettingsAsync(request, ct);
}
