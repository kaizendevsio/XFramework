using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public static class ServiceTokenMetadataExtensions
{
    public static async Task AttachServiceTokenAsync(
        this QueryDescriptor descriptor,
        IServiceTokenProvider tokenProvider,
        string audience,
        CancellationToken ct = default)
    {
        descriptor.Metadata ??= new RequestMetadata();
        descriptor.Metadata.RequestId ??= Guid.NewGuid();
        IReadOnlyCollection<string> scopes = descriptor.IgnoreQueryFilters
            ? [XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextQueryAllTenants]
            : [XFrameworkServiceScopes.DataContextQuery];
        descriptor.Metadata.ServiceAccessToken = await tokenProvider.GetTokenAsync(audience, scopes, ct);
    }

    public static async Task AttachServiceTokenAsync(
        this SaveChangesRequest request,
        IServiceTokenProvider tokenProvider,
        string audience,
        CancellationToken ct = default)
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.RequestId ??= Guid.NewGuid();
        request.Metadata.ServiceAccessToken = await tokenProvider.GetTokenAsync(
            audience,
            [XFrameworkServiceScopes.DataContextMutate],
            ct);
    }

    public static string ResolveCanonicalAudience(string targetClient)
    {
        var trimmed = targetClient.Trim();
        return XFrameworkServiceNames.All.FirstOrDefault(name =>
            string.Equals(name, trimmed, StringComparison.Ordinal) ||
            string.Equals(name.ToSha256(), trimmed, StringComparison.OrdinalIgnoreCase))
            ?? trimmed;
    }
}
