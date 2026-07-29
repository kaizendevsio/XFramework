using Bolt.Client;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public interface IBoltServiceInvocationAuthorizer
{
    Task<TrustedServiceInvocationResult> AuthorizeAsync(
        RequestMetadata? metadata,
        BoltInboundRequestContext requestContext,
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null,
        CancellationToken ct = default);
}
