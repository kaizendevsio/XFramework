using System.Security.Claims;
using Communications.Api.Services;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class CommunicationsRequestContextResolverTests
{
    private const string CommunicationsClientName = "XFramework.Communications";

    [Test]
    public async Task ResolveAsync_ServiceTokenMetadata_ReturnsUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidPortalToken);
        var resolver = Resolver();

        var result = await resolver.ResolveAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task ResolveTrustedInternalAsync_ServiceTokenMetadata_ReturnsContext()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidPortalToken);
        var resolver = Resolver();

        var result = await resolver.ResolveTrustedInternalAsync(metadata);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
        Assert.That(result.Data.IsTrustedInternal, Is.True);
    }

    [Test]
    public async Task ResolveAdminAsync_PortalServiceTokenMetadata_ReturnsAdminContext()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidPortalToken);
        var resolver = Resolver();

        var result = await resolver.ResolveAdminAsync(metadata);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
        Assert.That(result.Data.IsTrustedInternal, Is.True);
        Assert.That(result.Data.TrustedServiceName, Is.EqualTo("XFramework.Portal"));
    }

    [Test]
    public async Task ResolveTrustedInternalAsync_ServiceTokenWithWrongAudience_ReturnsUnauthorized()
    {
        var metadata = Metadata(
            Guid.NewGuid(),
            Guid.NewGuid(),
            token: FakeTrustedServiceInvocationResolver.WrongAudienceToken);
        var resolver = Resolver();

        var result = await resolver.ResolveTrustedInternalAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task ResolveAdminAsync_ServiceTokenWithWrongServiceName_ReturnsForbidden()
    {
        var metadata = Metadata(Guid.NewGuid(), Guid.NewGuid(), token: FakeTrustedServiceInvocationResolver.OtherServiceToken);
        var resolver = Resolver();

        var result = await resolver.ResolveAdminAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ResolveAsync_UnsignedInternalMetadata_ReturnsUnauthorized()
    {
        var metadata = Metadata(Guid.NewGuid(), Guid.NewGuid());
        var resolver = Resolver();

        var result = await resolver.ResolveAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task ResolveAsync_AuthenticatedUserWithMismatchedTenantMetadata_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, credentialId)
            }
        };
        var metadata = Metadata(Guid.NewGuid(), credentialId);
        var resolver = Resolver(accessor);

        var result = await resolver.ResolveAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ResolveAsync_AuthenticatedUserWithMismatchedCredentialMetadata_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, Guid.NewGuid())
            }
        };
        var metadata = Metadata(tenantId, Guid.NewGuid());
        var resolver = Resolver(accessor);

        var result = await resolver.ResolveAsync(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void ResolveTrustedInternalAsync_CallerCancellation_IsPropagated()
    {
        var metadata = Metadata(
            Guid.NewGuid(),
            Guid.NewGuid(),
            token: FakeTrustedServiceInvocationResolver.ValidPortalToken);
        var resolver = new CommunicationsRequestContextResolver(
            new HttpContextAccessor(),
            Configuration(),
            serviceInvocationResolver: new CancelingTrustedServiceInvocationResolver());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = async () => await resolver.ResolveTrustedInternalAsync(
            metadata,
            ct: cancellation.Token);

        Assert.That(act, Throws.InstanceOf<OperationCanceledException>());
    }

    private static CommunicationsRequestContextResolver Resolver(HttpContextAccessor? accessor = null) =>
        new(
            accessor ?? new HttpContextAccessor(),
            Configuration(),
            serviceInvocationResolver: new FakeTrustedServiceInvocationResolver());

    private static RequestMetadata Metadata(Guid tenantId, Guid credentialId, string? token = null)
    {
        return new RequestMetadata
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            Name = "XFramework.Portal",
            ServiceAccessToken = token
        };
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid credentialId) =>
        new(new ClaimsIdentity(
            [
                new Claim("tenantId", tenantId.ToString("D")),
                new Claim(ClaimTypes.Name, credentialId.ToString("D"))
            ],
            "TestAuth"));

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ClientName"] = CommunicationsClientName
            })
            .Build();

    private sealed class CancelingTrustedServiceInvocationResolver : ITrustedServiceInvocationResolver
    {
        public Task<TrustedServiceInvocationResult> ResolveAsync(
            RequestMetadata? metadata,
            string expectedAudience,
            IReadOnlyCollection<string>? requiredScopes = null,
            IReadOnlyCollection<string>? allowedCallers = null,
            bool requireTenant = true,
            CancellationToken ct = default) =>
            Task.FromCanceled<TrustedServiceInvocationResult>(ct);
    }
}
