using Bolt.Client;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public interface IBoltServiceInvocationAuthorizer
{
    Task<TrustedInvocationResult> AuthorizeAsync(
        InvocationCredentials credentials,
        RequestMetadata metadata,
        BoltInboundRequestContext requestContext,
        InvocationAuthorizationPolicy policy,
        CancellationToken ct = default);
}
