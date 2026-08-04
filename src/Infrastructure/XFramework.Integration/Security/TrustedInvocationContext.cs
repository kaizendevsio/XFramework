using System.Collections.Frozen;

namespace XFramework.Integration.Security;

public sealed record TrustedActorIdentity
{
    public TrustedActorIdentity(
        Guid CredentialId,
        Guid? IdentityId,
        Guid TenantId,
        Guid SessionId,
        IReadOnlySet<string> Roles,
        IReadOnlySet<string> Capabilities,
        string GenerationId,
        DateTimeOffset ExpiresAtUtc)
    {
        this.CredentialId = CredentialId;
        this.IdentityId = IdentityId;
        this.TenantId = TenantId;
        this.SessionId = SessionId;
        this.Roles = Roles.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        this.Capabilities = Capabilities.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        this.GenerationId = GenerationId;
        this.ExpiresAtUtc = ExpiresAtUtc;
    }

    public Guid CredentialId { get; }
    public Guid? IdentityId { get; }
    public Guid TenantId { get; }
    public Guid SessionId { get; }
    public FrozenSet<string> Roles { get; }
    public FrozenSet<string> Capabilities { get; }
    public string GenerationId { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
}

public sealed record TrustedServiceIdentity
{
    public TrustedServiceIdentity(
        string ClientId,
        string Audience,
        IReadOnlySet<string> Scopes,
        string? GenerationId)
    {
        this.ClientId = ClientId;
        this.Audience = Audience;
        this.Scopes = Scopes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        this.GenerationId = GenerationId;
    }

    public string ClientId { get; }
    public string Audience { get; }
    public FrozenSet<string> Scopes { get; }
    public string? GenerationId { get; }
}

public sealed record TrustedInvocationContext(
    TrustedActorIdentity? Actor,
    TrustedServiceIdentity? Service,
    Guid? EffectiveTenantId,
    Guid? RequestedTargetTenantId,
    Guid CorrelationId);

public sealed record ActorIdentityValidationResult(
    bool IsValid,
    TrustedActorIdentity? Identity,
    string? Error,
    int StatusCode)
{
    public static ActorIdentityValidationResult Success(TrustedActorIdentity identity) =>
        new(true, identity, null, 200);

    public static ActorIdentityValidationResult Failure(string error, int statusCode = 401) =>
        new(false, null, error, statusCode);
}

public sealed record ServiceIdentityValidationResult(
    bool IsValid,
    TrustedServiceIdentity? Identity,
    string? Error,
    int StatusCode)
{
    public static ServiceIdentityValidationResult Success(TrustedServiceIdentity identity) =>
        new(true, identity, null, 200);

    public static ServiceIdentityValidationResult Failure(string error, int statusCode = 401) =>
        new(false, null, error, statusCode);
}

public sealed record TrustedInvocationResult(
    bool IsSuccess,
    TrustedInvocationContext? Context,
    string? Error,
    int StatusCode)
{
    public static TrustedInvocationResult Success(TrustedInvocationContext context) =>
        new(true, context, null, 200);

    public static TrustedInvocationResult Failure(string error, int statusCode = 401) =>
        new(false, null, error, statusCode);
}
