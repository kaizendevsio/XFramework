namespace XFramework.Integration.Security;

public readonly record struct InvocationPolicyCheckResult(
    bool IsSuccess,
    string? Error,
    int StatusCode)
{
    public static InvocationPolicyCheckResult Success() => new(true, null, 200);

    public static InvocationPolicyCheckResult Failure(string error, int statusCode) =>
        new(false, error, statusCode);
}

public static class TrustedActorPolicyEvaluator
{
    public static InvocationPolicyCheckResult Evaluate(
        TrustedActorIdentity? actor,
        InvocationAuthorizationPolicy policy)
    {
        if (policy.ActorRequirement == ActorRequirement.None && actor is not null)
        {
            return InvocationPolicyCheckResult.Failure(
                "This operation does not accept actor delegation.",
                403);
        }

        var hasActorRestrictions =
            policy.RequiredActorRoles.Count > 0 ||
            policy.RequiredActorCapabilities.Count > 0 ||
            policy.RequiredActorAttributes.Count > 0;
        if (actor is null &&
            (policy.ActorRequirement == ActorRequirement.Required || hasActorRestrictions))
        {
            return InvocationPolicyCheckResult.Failure("Actor identity is required.", 401);
        }

        if (actor is null)
            return InvocationPolicyCheckResult.Success();

        if (policy.RequiredActorRoles.Count > 0 &&
            !policy.RequiredActorRoles.Any(actor.Roles.Contains))
        {
            return InvocationPolicyCheckResult.Failure(
                "Actor is not authorized for this operation.",
                403);
        }

        if (policy.RequiredActorCapabilities.Any(capability =>
                !actor.Capabilities.Contains(capability)))
        {
            return InvocationPolicyCheckResult.Failure(
                "Actor is not authorized for this operation.",
                403);
        }

        if (policy.RequiredActorAttributes.Any(requirement =>
                !actor.Attributes.TryGetValue(requirement.Key, out var value) ||
                !string.Equals(value, requirement.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return InvocationPolicyCheckResult.Failure(
                "Actor is not authorized for this operation.",
                403);
        }

        return InvocationPolicyCheckResult.Success();
    }
}
