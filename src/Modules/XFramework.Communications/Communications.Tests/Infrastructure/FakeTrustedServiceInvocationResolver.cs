using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Tests.Infrastructure;

internal sealed class FakeTrustedServiceInvocationResolver : ITrustedServiceInvocationResolver
{
    public const string ValidControlPanelToken = "valid-control-panel-token";
    public const string OtherServiceToken = "valid-other-service-token";
    public const string WrongAudienceToken = "wrong-audience-token";

    public Task<TrustedServiceInvocationResult> ResolveAsync(
        RequestMetadata? metadata,
        string expectedAudience,
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null,
        bool requireTenant = true,
        CancellationToken ct = default)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.ServiceAccessToken))
            return Task.FromResult(TrustedServiceInvocationResult.Failure("Service token is required."));

        if (metadata.ServiceAccessToken == WrongAudienceToken)
            return Task.FromResult(TrustedServiceInvocationResult.Failure("Wrong audience."));

        var caller = metadata.ServiceAccessToken switch
        {
            ValidControlPanelToken => XFrameworkServiceNames.ControlPanel,
            OtherServiceToken => "XFramework.OtherService",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(caller))
            return Task.FromResult(TrustedServiceInvocationResult.Failure("Unknown test service token."));

        var scopes = new HashSet<string>(
            requiredScopes ?? [XFrameworkServiceScopes.BoltService],
            StringComparer.OrdinalIgnoreCase);

        var invocation = new TrustedServiceInvocation(
            caller,
            expectedAudience,
            metadata.TenantId,
            metadata.CredentialId,
            metadata,
            scopes);

        return Task.FromResult(TrustedServiceInvocationResult.Success(invocation));
    }
}
