namespace XFramework.Integration.Security;

public interface ITrustedServiceTargetContextInitializer
{
    Task<TrustedInvocationResult> EstablishAsync(
        Guid targetTenantId,
        string audience,
        IReadOnlyCollection<string> requiredServiceScopes,
        string allowedServiceCaller,
        Guid? correlationId = null,
        CancellationToken ct = default);
}
