using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using POS.Api.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosRequestContextResolverTests
{
    [Test]
    public void Resolve_HttpClaimTenantMismatch_ReturnsForbidden()
    {
        var trustedTenantId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                TenantId = Guid.NewGuid()
            }
        };
        var resolver = new PosRequestContextResolver(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(("tenant_id", trustedTenantId.ToString()))
                }
            },
            Configuration());

        var result = resolver.Resolve(request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public void Resolve_TargetCashierMismatchForNonPrivilegedUser_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                TenantId = tenantId,
                CredentialId = actorCredentialId
            }
        };
        var resolver = new PosRequestContextResolver(
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(
                        ("tenant_id", tenantId.ToString()),
                        ("credential_id", actorCredentialId.ToString()))
                }
            },
            Configuration());

        var result = resolver.Resolve(request, requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public void Resolve_TrustedInternalMetadata_SanitizesTenantAndCredential()
    {
        var tenantId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                TenantId = tenantId,
                CredentialId = actorCredentialId
            }
        };
        var resolver = new PosRequestContextResolver(
            new HttpContextAccessor(),
            Configuration(),
            new FakeTrustedServiceInvocationResolver(tenantId, actorCredentialId));

        var result = resolver.Resolve(request);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.TenantId.Should().Be(tenantId);
        result.Data.ActorCredentialId.Should().Be(actorCredentialId);
        result.Data.IsTrustedInternal.Should().BeTrue();
        request.Metadata.TenantId.Should().Be(tenantId);
        request.Metadata.CredentialId.Should().Be(actorCredentialId);
        request.Metadata.RequestId.Should().NotBeNull();
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ClientName"] = XFrameworkServiceNames.Pos
            })
            .Build();

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "Test"));

    private sealed class FakeTrustedServiceInvocationResolver(Guid tenantId, Guid actorCredentialId)
        : ITrustedServiceInvocationResolver
    {
        public Task<TrustedServiceInvocationResult> ResolveAsync(
            RequestMetadata? metadata,
            string expectedAudience,
            IReadOnlyCollection<string>? requiredScopes = null,
            IReadOnlyCollection<string>? allowedCallers = null,
            bool requireTenant = true,
            CancellationToken ct = default)
        {
            var trustedMetadata = new RequestMetadata
            {
                TenantId = tenantId,
                CredentialId = actorCredentialId,
                RequestId = Guid.NewGuid()
            };

            return Task.FromResult(TrustedServiceInvocationResult.Success(new TrustedServiceInvocation(
                XFrameworkServiceNames.Portal,
                expectedAudience,
                tenantId,
                actorCredentialId,
                trustedMetadata,
                requiredScopes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [])));
        }
    }
}
