using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Logout;

public static class LogoutEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/logout", Tags = ["Auth"],
        Summary = "Logout a user",
        Description = "Marks the user's session as Inactive. Creates an authorization log entry for audit trail.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.LogoutAsync(request, ct);
    }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");
    }
}
