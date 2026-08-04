using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Tests.Infrastructure;

internal sealed class FakeTrustedServiceInvocationResolver(
    Guid? tenantId = null,
    Guid? credentialId = null,
    string serviceName = XFrameworkServiceNames.Portal,
    bool includeActor = false,
    IReadOnlySet<string>? roles = null) : ITrustedInvocationContextAccessor
{
    public const string ValidPortalToken = "valid-portal-token";
    public const string WrongAudienceToken = "wrong-audience-token";
    public const string OtherServiceToken = "other-service-token";

    private readonly Guid resolvedTenantId = tenantId ?? Guid.NewGuid();

    public TrustedInvocationContext Current => new(
        includeActor
            ? new TrustedActorIdentity(
                credentialId ?? Guid.NewGuid(),
                Guid.NewGuid(),
                resolvedTenantId,
                Guid.NewGuid(),
                roles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "test-generation",
                DateTimeOffset.UtcNow.AddHours(1))
            : null,
        new TrustedServiceIdentity(
            serviceName,
            XFrameworkServiceNames.Communications,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { XFrameworkServiceScopes.BoltService },
            "test-service-generation"),
        resolvedTenantId,
        resolvedTenantId,
        Guid.NewGuid());
}
