using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;

namespace ControlPanel.Server.Services;

public sealed class MessagingControlPanelSettingsService(
    IMessagingServiceWrapper messaging,
    RequestMetadata metadata,
    TenantFilterService tenantFilter)
{
    public async Task<MessagingSettingsLoadResult> GetSettingsAsync(CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingSettingsLoadResult.Failure("Select a tenant before loading Messaging settings.");
        }

        var response = await messaging.GetMessagingSettingsAsync(
            new GetMessagingSettingsRequest
            {
                Metadata = BuildMetadata(tenantId)
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? MessagingSettingsLoadResult.Success(response.Response)
            : MessagingSettingsLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Messaging settings could not be loaded."));
    }

    public async Task<MessagingSettingsLoadResult> UpdateSettingsAsync(
        IEnumerable<UpdateMessagingSettingValueRequest> values,
        CancellationToken ct = default)
    {
        if (tenantFilter.SelectedTenantId is not Guid tenantId)
        {
            return MessagingSettingsLoadResult.Failure("Select a tenant before saving Messaging settings.");
        }

        var response = await messaging.UpdateMessagingSettingsAsync(
            new UpdateMessagingSettingsRequest
            {
                Metadata = BuildMetadata(tenantId),
                Values = values.ToList()
            },
            ct);

        return response is { IsSuccess: true, Response: not null }
            ? MessagingSettingsLoadResult.Success(response.Response, response.Message ?? "Messaging settings saved.")
            : MessagingSettingsLoadResult.Failure(NormalizeFailureMessage(
                response.Message,
                "Messaging settings could not be saved."));
    }

    private RequestMetadata BuildMetadata(Guid tenantId) => new()
    {
        TenantId = tenantId,
        CredentialId = metadata.CredentialId,
        SessionId = metadata.SessionId,
        RequestId = Guid.NewGuid(),
        Name = metadata.Name,
        DeviceName = metadata.DeviceName,
        DeviceAgent = metadata.DeviceAgent,
        IpAddress = metadata.IpAddress
    };

    private static string NormalizeFailureMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallback;
        }

        return string.Equals(message, "NotFound", StringComparison.OrdinalIgnoreCase)
            ? "Messaging settings service is unavailable. Check Messaging service health and Bolt handler registration."
            : message;
    }
}

public sealed record MessagingSettingsLoadResult(
    bool IsSuccess,
    MessagingSettingsResponse? Settings,
    string Message)
{
    public static MessagingSettingsLoadResult Success(
        MessagingSettingsResponse settings,
        string message = "Messaging settings loaded.") =>
        new(true, settings, message);

    public static MessagingSettingsLoadResult Failure(string message) =>
        new(false, null, message);
}
