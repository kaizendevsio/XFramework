namespace IdentityServer.Api.Services;

public interface IIdentityAdministrationService
{
    Task<Result<IdentityAdministrationResponse>> CreateAsync(CreateIdentityRequest request, CancellationToken ct);

    Task<Result<IdentityAdministrationResponse>> UpdateProfileAsync(
        UpdateIdentityProfileRequest request,
        CancellationToken ct);

    Task<Result<IdentityAdministrationResponse>> SetEnabledAsync(
        SetIdentityEnabledRequest request,
        CancellationToken ct);

    Task<Result> SoftDeleteAsync(SoftDeleteIdentityRequest request, CancellationToken ct);
}
