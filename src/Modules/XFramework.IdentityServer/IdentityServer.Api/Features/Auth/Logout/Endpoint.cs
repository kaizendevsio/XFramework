using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Logout;

public static class LogoutEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result> Handle(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.LogoutAsync(request, ct);

    [MapPost("/api/auth/logout", Tags = ["Auth"],
        Summary = "Logout a user",
        Description = "Marks the user's session as Inactive. Creates an authorization log entry for audit trail.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        LogoutRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.LogoutAsync(request, ct);
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
