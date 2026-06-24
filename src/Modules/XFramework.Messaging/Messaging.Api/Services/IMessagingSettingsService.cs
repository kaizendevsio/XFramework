using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Messaging.Api.Services;

public interface IMessagingSettingsService
{
    Task<Result<MessagingSettingsResponse>> GetSettingsAsync(
        GetMessagingSettingsRequest request,
        CancellationToken ct = default);

    Task<Result<MessagingSettingsResponse>> UpdateSettingsAsync(
        UpdateMessagingSettingsRequest request,
        CancellationToken ct = default);
}
