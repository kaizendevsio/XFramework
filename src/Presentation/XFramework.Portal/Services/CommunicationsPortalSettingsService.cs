using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Portal.Services;

public sealed class CommunicationsPortalSettingsService(
    ICommunicationsServiceWrapper communications,
    RequestMetadata metadata,
    TenantFilterService tenantFilter)
{
    public async Task<CommunicationsSettingsLoadResult> GetSettingsAsync(CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsSettingsLoadResult.Failure("Select a tenant before loading Communications settings.");
        }

        var response = await communications.GetCommunicationsSettingsAsync(
            new GetCommunicationsSettingsRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? CommunicationsSettingsLoadResult.Success(response.Response)
            : CommunicationsSettingsLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Communications settings could not be loaded."));
    }

    public async Task<CommunicationsSettingsLoadResult> UpdateSettingsAsync(
        IEnumerable<UpdateCommunicationsSettingValueRequest> values,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return CommunicationsSettingsLoadResult.Failure("Select a tenant before saving Communications settings.");
        }

        var response = await communications.UpdateCommunicationsSettingsAsync(
            new UpdateCommunicationsSettingsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Values = values.ToList()
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? CommunicationsSettingsLoadResult.Success(response.Response, response.Message ?? "Communications settings saved.")
            : CommunicationsSettingsLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Communications settings could not be saved."));
    }

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        RequestedTenantId = tenantId,
        RequestId = Guid.NewGuid(),
        OperationName = "Portal",
        DeviceName = metadata.DeviceName,
        UserAgent = metadata.UserAgent,
        IpAddress = metadata.IpAddress
    };

    private static string NormalizeFailureMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallback;
        }

        return string.Equals(message, "NotFound", StringComparison.OrdinalIgnoreCase)
            ? "Communications settings service is unavailable. Check Communications service health and Bolt handler registration."
            : message;
    }
}

public sealed record CommunicationsSettingsLoadResult(
    bool IsSuccess,
    CommunicationsSettingsResponse? Settings,
    string Message)
{
    public static CommunicationsSettingsLoadResult Success(
        CommunicationsSettingsResponse settings,
        string message = "Communications settings loaded.") =>
        new(true, settings, message);

    public static CommunicationsSettingsLoadResult Failure(string message) =>
        new(false, null, message);
}
