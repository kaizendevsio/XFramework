using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.ValidateSession;

public static class ValidateIdentitySessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentitySessionValidate])]
    public static Task<Result<ValidateIdentitySessionResponse>> Handle(
        ValidateIdentitySessionRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.ValidateIdentitySessionAsync(request, ct);

    [MapPost("/api/auth/validate-session", Tags = ["Auth"],
        Summary = "Validate an issued identity session",
        Description = "Fail-closed validation of the session and all identity lifecycle dependencies.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = false)]
    public static Task<Result<ValidateIdentitySessionResponse>> HandleHttp(
        ValidateIdentitySessionRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        if (!TryGetGuidClaim(httpContext.User, "tenant_id", out var tenantId)
            || !TryGetGuidClaim(httpContext.User, "credential_id", out var credentialId)
            || !TryGetGuidClaim(httpContext.User, "session_id", out var sessionId)
            || !TryGetRoleTypeIds(httpContext.User, out var roleTypeIds))
        {
            return Task.FromResult(
                Result<ValidateIdentitySessionResponse>.Forbidden(
                    "Authenticated session claims are incomplete."));
        }

        request.TenantId = tenantId;
        request.CredentialId = credentialId;
        request.SessionId = sessionId;
        request.RoleTypeIds = roleTypeIds;
        return authService.ValidateIdentitySessionAsync(request, ct);
    }

    private static bool TryGetGuidClaim(
        System.Security.Claims.ClaimsPrincipal principal,
        string claimType,
        out Guid value) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out value) && value != Guid.Empty;

    private static bool TryGetRoleTypeIds(
        System.Security.Claims.ClaimsPrincipal principal,
        out List<Guid> roleTypeIds)
    {
        roleTypeIds = [];
        var roleClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(roleClaim))
            return false;

        try
        {
            roleTypeIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(roleClaim) ?? [];
            return roleTypeIds.Count > 0 && roleTypeIds.All(roleTypeId => roleTypeId != Guid.Empty);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}

public sealed class ValidateIdentitySessionRequestValidator : AbstractValidator<ValidateIdentitySessionRequest>
{
    public ValidateIdentitySessionRequestValidator()
    {
        RuleFor(request => request.TenantId).NotEmpty();
        RuleFor(request => request.CredentialId).NotEmpty();
        RuleFor(request => request.SessionId).NotEmpty();
        RuleFor(request => request.RoleTypeIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(roleTypeIds => roleTypeIds.Count <= 64)
            .WithMessage("At most 64 role types can be validated at once");
        RuleForEach(request => request.RoleTypeIds)
            .NotEmpty()
            .When(request => request.RoleTypeIds is not null);
    }
}
