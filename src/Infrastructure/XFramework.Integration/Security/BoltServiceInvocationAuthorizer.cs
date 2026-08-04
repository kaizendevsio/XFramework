using Bolt.Client;
using Bolt.Protocol;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public sealed class BoltServiceInvocationAuthorizer(
    ITrustedInvocationResolver invocationResolver,
    IServiceIdentityProvider serviceIdentityProvider,
    ITrustedInvocationContextStore contextStore,
    IOptions<ServiceIdentityOptions> serviceIdentityOptions)
    : IBoltServiceInvocationAuthorizer
{
    public async Task<TrustedInvocationResult> AuthorizeAsync(
        InvocationCredentials credentials,
        RequestMetadata metadata,
        BoltInboundRequestContext requestContext,
        InvocationAuthorizationPolicy policy,
        CancellationToken ct = default)
    {
        var expectedAudience = serviceIdentityOptions.Value.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(expectedAudience))
            throw new InvalidOperationException("ServiceIdentity:ClientId is required for Bolt handler authorization.");

        if (!string.IsNullOrWhiteSpace(credentials.ServiceAccessToken))
        {
            var serviceValidation = await serviceIdentityProvider.ValidateAsync(
                credentials.ServiceAccessToken,
                expectedAudience,
                ct);
            if (!serviceValidation.IsValid)
                return TrustedInvocationResult.Failure(serviceValidation.Error!, serviceValidation.StatusCode);

            var expectedSenderHash = BoltCodec.Fnv1aHash(
                serviceValidation.Identity!.ClientId.ToSha256());
            if (expectedSenderHash != requestContext.SenderHash)
            {
                return TrustedInvocationResult.Failure(
                    "Service token caller does not match the authenticated Bolt sender.",
                    403);
            }
        }
        else if (!policy.AllowAnonymous)
        {
            return TrustedInvocationResult.Failure("Bolt invocations require a service identity.");
        }

        var authorization = await invocationResolver.ResolveAsync(
            credentials,
            metadata,
            policy,
            expectedAudience,
            ct);
        if (!authorization.IsSuccess)
            return authorization;

        contextStore.Set(authorization.Context!);
        return authorization;
    }
}
