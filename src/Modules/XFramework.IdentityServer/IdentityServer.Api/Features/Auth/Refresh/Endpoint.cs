using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Refresh;

public static class RefreshTokenEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/refresh", Tags = ["Auth"],
        Summary = "Refresh an access token",
        Description = "Validates the refresh token against stored session data, generates a new token pair, and updates the session.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.RefreshTokenAsync(request, ct);
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");

        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");
    }
}
