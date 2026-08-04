using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.Auth.ValidateSession;

public static class ValidateIdentitySessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentitySessionValidate])]
    public static Task<Result<ValidateIdentitySessionResponse>> Handle(
        ValidateIdentitySessionRequest request,
        ITrustedInvocationContextAccessor invocationContextAccessor,
        CancellationToken ct) => Task.FromResult(ToResponse(invocationContextAccessor));

    [MapPost("/api/auth/validate-session", Tags = ["Auth"],
        Summary = "Validate an issued identity session",
        Description = "Fail-closed validation of the session and all identity lifecycle dependencies.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = false)]
    public static Task<Result<ValidateIdentitySessionResponse>> HandleHttp(
        ValidateIdentitySessionRequest request,
        ITrustedInvocationContextAccessor invocationContextAccessor,
        CancellationToken ct)
        => Task.FromResult(ToResponse(invocationContextAccessor));

    private static Result<ValidateIdentitySessionResponse> ToResponse(
        ITrustedInvocationContextAccessor invocationContextAccessor)
    {
        var actor = invocationContextAccessor.Current?.Actor;
        if (actor is null)
            return Result<ValidateIdentitySessionResponse>.Unauthorized("Actor identity is required.");

        return Result<ValidateIdentitySessionResponse>.Success(new ValidateIdentitySessionResponse
        {
            TenantId = actor.TenantId,
            CredentialId = actor.CredentialId,
            IdentityId = actor.IdentityId ?? Guid.Empty,
            SessionId = actor.SessionId,
            Roles = actor.Roles.ToList(),
            Capabilities = actor.Capabilities.ToList(),
            GenerationId = actor.GenerationId,
            ExpiresAtUtc = actor.ExpiresAtUtc.UtcDateTime,
            IsValid = true
        });
    }
}
