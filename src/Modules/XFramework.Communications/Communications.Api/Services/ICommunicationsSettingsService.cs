using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public interface ICommunicationsSettingsService
{
    Task<Result<CommunicationsSettingsResponse>> GetSettingsAsync(
        GetCommunicationsSettingsRequest request,
        CancellationToken ct = default);

    Task<Result<CommunicationsSettingsResponse>> UpdateSettingsAsync(
        UpdateCommunicationsSettingsRequest request,
        CancellationToken ct = default);
}
