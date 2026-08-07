using System.Collections.Frozen;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Security;

namespace XFramework.Core.DataContext;

public enum GeneratedEntityOperation
{
    Read,
    Create,
    Update,
    Delete
}

public sealed record GeneratedEntityAuthorizationPolicy
{
    public required string EntityTypeName { get; init; }
    public required GeneratedEntityOperation Operation { get; init; }
    public bool RequireAuthorization { get; init; } = true;
    public ActorRequirement ActorRequirement { get; init; } = ActorRequirement.Required;
    public TenantAccessMode TenantAccessMode { get; init; } = TenantAccessMode.ActorTenant;
    public string? AuthorizationFeature { get; init; }
    public string? RequiredCapability { get; init; }
    public IReadOnlyCollection<string> RequiredCrossTenantActorCapabilities { get; init; } =
        ["identity.tenants:manage"];
    public IReadOnlyCollection<string> RequiredRoles { get; init; } = [];
    public IReadOnlyDictionary<string, string> RequiredActorAttributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool AllowRemoteQuery { get; init; } = true;
    public bool AllowRemoteMutation { get; init; }
    public bool AllowServiceOnly { get; init; }
    public IReadOnlyCollection<string> AllowedServiceCallers { get; init; } = [];
    public IReadOnlyCollection<string> RequiredServiceScopes { get; init; } = [];
    public int PolicyVersion { get; init; } = GeneratedAuthorizationPolicyVersion.Current;

    public InvocationAuthorizationPolicy ToActorPolicy() => new()
    {
        ActorRequirement = RequireAuthorization ? ActorRequirement : ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode,
        RequireServiceIdentity = false,
        RequiredActorRoles = RequiredRoles,
        RequiredActorCapabilities = string.IsNullOrWhiteSpace(RequiredCapability)
            ? []
            : [RequiredCapability],
        RequiredActorAttributes = RequiredActorAttributes,
        RequiredCrossTenantActorCapabilities = RequiredCrossTenantActorCapabilities
    };
}

public sealed class GeneratedEntityAuthorizationPolicyRegistry
{
    private readonly FrozenDictionary<(string Entity, GeneratedEntityOperation Operation), GeneratedEntityAuthorizationPolicy> _policies;

    public GeneratedEntityAuthorizationPolicyRegistry(
        IEnumerable<GeneratedEntityAuthorizationPolicy>? policies = null)
    {
        _policies = (policies ?? [])
            .Select(Normalize)
            .ToFrozenDictionary(
                policy => (policy.EntityTypeName.ToUpperInvariant(), policy.Operation));
    }

    public bool TryGet(
        string entityTypeName,
        GeneratedEntityOperation operation,
        out GeneratedEntityAuthorizationPolicy policy) =>
        _policies.TryGetValue((entityTypeName.ToUpperInvariant(), operation), out policy!);

    public IReadOnlyDictionary<(string Entity, GeneratedEntityOperation Operation), GeneratedEntityAuthorizationPolicy> Snapshot() =>
        _policies;

    private static GeneratedEntityAuthorizationPolicy Normalize(GeneratedEntityAuthorizationPolicy policy) =>
        policy with
        {
            RequiredRoles = policy.RequiredRoles.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            RequiredActorAttributes = policy.RequiredActorAttributes
                .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            RequiredCrossTenantActorCapabilities = policy.RequiredCrossTenantActorCapabilities
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            AllowedServiceCallers = policy.AllowedServiceCallers.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            RequiredServiceScopes = policy.RequiredServiceScopes.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
        };
}

public static class GeneratedEntityAuthorizationEvaluator
{
    public static InvocationPolicyCheckResult Evaluate(
        TrustedInvocationContext? context,
        GeneratedEntityAuthorizationPolicy policy)
    {
        if (context is null)
            return InvocationPolicyCheckResult.Failure("A trusted invocation context is required.", 401);

        if (context.Actor is { } actor)
        {
            var actorResult = TrustedActorPolicyEvaluator.Evaluate(actor, policy.ToActorPolicy());
            if (!actorResult.IsSuccess)
                return actorResult;

            if (policy.TenantAccessMode == TenantAccessMode.Tenantless)
            {
                return context.EffectiveTenantId is null
                    ? InvocationPolicyCheckResult.Success()
                    : InvocationPolicyCheckResult.Failure("This operation does not accept a target tenant.", 403);
            }

            if (context.EffectiveTenantId is not { } effectiveTenantId)
                return InvocationPolicyCheckResult.Failure("A trusted tenant is required.", 403);

            if (effectiveTenantId == actor.TenantId)
                return InvocationPolicyCheckResult.Success();

            if (policy.TenantAccessMode != TenantAccessMode.DelegatedTenant ||
                policy.RequiredCrossTenantActorCapabilities.Count == 0 ||
                policy.RequiredCrossTenantActorCapabilities.Any(capability =>
                    !actor.Capabilities.Contains(capability)))
            {
                return InvocationPolicyCheckResult.Failure(
                    "Actor is not authorized for delegated tenant access.",
                    403);
            }

            return InvocationPolicyCheckResult.Success();
        }

        if (!policy.AllowServiceOnly || context.Service is not { } service)
            return InvocationPolicyCheckResult.Failure("Actor identity is required.", 401);

        if (policy.AllowedServiceCallers.Count == 0 ||
            !policy.AllowedServiceCallers.Contains(service.ClientId, StringComparer.OrdinalIgnoreCase))
        {
            return InvocationPolicyCheckResult.Failure(
                "Service caller is not allowed for this entity operation.",
                403);
        }

        if (policy.RequiredServiceScopes.Any(scope => !service.Scopes.Contains(scope)))
        {
            return InvocationPolicyCheckResult.Failure(
                "Service token is missing a required entity-operation scope.",
                403);
        }

        return context.EffectiveTenantId is not null || policy.TenantAccessMode == TenantAccessMode.Tenantless
            ? InvocationPolicyCheckResult.Success()
            : InvocationPolicyCheckResult.Failure("A trusted target tenant is required.", 403);
    }
}
