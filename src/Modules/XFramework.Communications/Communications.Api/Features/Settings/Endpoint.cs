using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Settings;

public static class GetCommunicationsSettingsEndpoint
{
    [BoltHandler]
    [MapGet("/api/communications/settings", Tags = ["Communications Settings"],
        Summary = "Get Communications tenant settings",
        Description = "Returns grouped Communications settings for the authenticated tenant, including stored values and defaults.")]
    public static Task<Result<CommunicationsSettingsResponse>> Handle(
        GetCommunicationsSettingsRequest request,
        ICommunicationsSettingsService settingsService,
        CancellationToken ct) =>
        settingsService.GetSettingsAsync(request, ct);
}

public static class UpdateCommunicationsSettingsEndpoint
{
    [BoltHandler]
    [MapPut("/api/communications/settings", Tags = ["Communications Settings"],
        Summary = "Update Communications tenant settings",
        Description = "Validates and persists tenant-scoped Communications settings as RegistryConfiguration rows.")]
    public static Task<Result<CommunicationsSettingsResponse>> Handle(
        UpdateCommunicationsSettingsRequest request,
        ICommunicationsSettingsService settingsService,
        CancellationToken ct) =>
        settingsService.UpdateSettingsAsync(request, ct);
}
