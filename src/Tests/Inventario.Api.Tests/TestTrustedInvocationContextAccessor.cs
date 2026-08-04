using XFramework.Integration.Security;

namespace Inventario.Api.Tests;

internal sealed class TestTrustedInvocationContextAccessor(
    Guid? tenantId,
    IReadOnlySet<string>? roles = null,
    IReadOnlySet<string>? capabilities = null) : ITrustedInvocationContextAccessor
{
    public TrustedInvocationContext? Current { get; } = tenantId is { } value && value != Guid.Empty
        ? new TrustedInvocationContext(
            new TrustedActorIdentity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                value,
                Guid.NewGuid(),
                roles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                capabilities ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "test-generation",
                DateTimeOffset.UtcNow.AddHours(1)),
            Service: null,
            EffectiveTenantId: value,
            RequestedTargetTenantId: null,
            CorrelationId: Guid.NewGuid())
        : null;
}
