using System.Security.Claims;
using Messaging.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;

namespace Messaging.Tests.Services;

public sealed class MessagingRequestContextResolverTests
{
    private const string TrustedMetadataSecret = "messaging-context-test-secret";

    [Test]
    public void Resolve_SignedInternalMetadata_ReturnsUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, sign: true);
        var resolver = new MessagingRequestContextResolver(new HttpContextAccessor(), Configuration());

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void ResolveTrustedInternal_SignedInternalMetadata_ReturnsContext()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, sign: true);
        var resolver = new MessagingRequestContextResolver(new HttpContextAccessor(), Configuration());

        var result = resolver.ResolveTrustedInternal(metadata);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
        Assert.That(result.Data.IsTrustedInternal, Is.True);
    }

    [Test]
    public void Resolve_UnsignedInternalMetadata_ReturnsUnauthorized()
    {
        var metadata = Metadata(Guid.NewGuid(), Guid.NewGuid(), sign: false);
        var resolver = new MessagingRequestContextResolver(new HttpContextAccessor(), Configuration());

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void Resolve_AuthenticatedUserWithMismatchedTenantMetadata_ReturnsForbidden()
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
        var metadata = Metadata(Guid.NewGuid(), credentialId, sign: false);
        var resolver = new MessagingRequestContextResolver(accessor, Configuration());

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void Resolve_AuthenticatedUserWithMismatchedCredentialMetadata_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, Guid.NewGuid())
            }
        };
        var metadata = Metadata(tenantId, Guid.NewGuid(), sign: false);
        var resolver = new MessagingRequestContextResolver(accessor, Configuration());

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    private static RequestMetadata Metadata(Guid tenantId, Guid credentialId, bool sign)
    {
        var metadata = new RequestMetadata
        {
            TenantId = tenantId,
            CredentialId = credentialId
        };

        if (sign)
            RequestMetadataTrust.Sign(metadata, TrustedMetadataSecret);

        return metadata;
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
                ["Messaging:TrustedMetadata:SharedSecret"] = TrustedMetadataSecret
            })
            .Build();
}
