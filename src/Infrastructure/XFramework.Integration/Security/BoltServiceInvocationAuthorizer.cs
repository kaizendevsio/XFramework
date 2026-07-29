using Bolt.Client;
using Bolt.Protocol;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public sealed class BoltServiceInvocationAuthorizer(
    ITrustedServiceInvocationResolver invocationResolver,
    IOptions<ServiceIdentityOptions> serviceIdentityOptions)
    : IBoltServiceInvocationAuthorizer
{
    public async Task<TrustedServiceInvocationResult> AuthorizeAsync(
        RequestMetadata? metadata,
        BoltInboundRequestContext requestContext,
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null,
        CancellationToken ct = default)
    {
        var expectedAudience = serviceIdentityOptions.Value.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(expectedAudience))
            throw new InvalidOperationException("ServiceIdentity:ClientId is required for Bolt handler authorization.");

        var authorization = await invocationResolver.ResolveAsync(
            metadata,
            expectedAudience,
            requiredScopes,
            allowedCallers,
            requireTenant: false,
            ct);
        if (!authorization.IsSuccess)
            return authorization;

        var expectedSenderHash = BoltCodec.Fnv1aHash(
            authorization.Invocation!.CallerClientId.ToSha256());
        return expectedSenderHash == requestContext.SenderHash
            ? authorization
            : TrustedServiceInvocationResult.Failure(
                "Service token caller does not match the authenticated Bolt sender.",
                403);
    }
}
