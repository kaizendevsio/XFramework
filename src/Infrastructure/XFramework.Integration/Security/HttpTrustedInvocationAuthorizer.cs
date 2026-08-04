using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public interface IHttpTrustedInvocationAuthorizer
{
    Task<TrustedInvocationResult> AuthorizeAsync(
        string? authorizationHeader,
        string? serviceAuthorizationHeader,
        RequestMetadata metadata,
        InvocationAuthorizationPolicy policy,
        CancellationToken ct = default);
}

public sealed class HttpTrustedInvocationAuthorizer(
    ITrustedInvocationResolver resolver,
    ITrustedInvocationContextStore contextStore,
    IOptions<ServiceIdentityOptions> serviceIdentityOptions)
    : IHttpTrustedInvocationAuthorizer
{
    public async Task<TrustedInvocationResult> AuthorizeAsync(
        string? authorizationHeader,
        string? serviceAuthorizationHeader,
        RequestMetadata metadata,
        InvocationAuthorizationPolicy policy,
        CancellationToken ct = default)
    {
        var actorToken = ReadBearerToken(authorizationHeader);
        var serviceToken = ReadBearerToken(serviceAuthorizationHeader);
        var expectedAudience = serviceIdentityOptions.Value.ClientId?.Trim() ?? string.Empty;

        var authorization = await resolver.ResolveAsync(
            new InvocationCredentials(actorToken, serviceToken),
            metadata,
            policy,
            expectedAudience,
            ct);

        if (authorization.IsSuccess)
            contextStore.Set(authorization.Context!);

        return authorization;
    }

    private static string? ReadBearerToken(string? value)
    {
        const string prefix = "Bearer ";
        return value?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
            ? value[prefix.Length..].Trim()
            : null;
    }
}
