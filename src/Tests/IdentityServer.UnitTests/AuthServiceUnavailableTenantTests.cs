using FluentAssertions;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Storage.Integration.Drivers;
using XFramework.Core.RateLimiting;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;
using XFramework.Integration.Services;

namespace IdentityServer.UnitTests;

/// <summary>
/// Signing in to a tenant that was deleted, disabled or has expired is a rejected sign-in, not a server fault. It used to
/// answer 500 ("An error occurred during authentication"), which made a deleted tenant look like a broken release: the
/// xeon-dev deploy smoke rolled back a healthy candidate on BOLT_PHASE0_REFRESH_HTTP_STATUS_500 after its synthetic
/// tenant was soft-deleted in Portal.
/// </summary>
[TestFixture]
public sealed class AuthServiceUnavailableTenantTests
{
    [Test]
    public async Task Authenticate_WhenTenantIsUnavailable_IsRejectedAsInvalidCredentials()
    {
        var tenantId = Guid.NewGuid();
        var tenants = new Mock<ITenantResolver>();
        tenants.Setup(resolver => resolver.GetTenant(tenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TenantUnavailableException(tenantId));
        using var dbContext = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(
            Mock.Of<IDataContext>(),
            dbContext,
            Mock.Of<IServiceScopeFactory>(),
            new HttpContextAccessor(),
            tenants.Object,
            Mock.Of<IJwtService>(),
            TimeProvider.System,
            new AllowingRateLimiter(),
            new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
                Actor: null,
                Service: new TrustedServiceIdentity(
                    "trusted-portal",
                    XFrameworkServiceNames.IdentityServer,
                    new HashSet<string>(),
                    GenerationId: null),
                EffectiveTenantId: tenantId,
                RequestedTargetTenantId: tenantId,
                CorrelationId: Guid.NewGuid())),
            new CacheManager(memoryCache, NullLogger<CacheManager>.Instance),
            Mock.Of<IStorageServiceWrapper>(),
            Mock.Of<IIdentityAuthorizationService>(),
            NullLogger<AuthService>.Instance);

        var result = await service.AuthenticateAsync(new AuthenticateIdentityRequest
        {
            UserName = "synthetic-user",
            Password = "ValidPassword123!",
            RoleId = Guid.NewGuid(),
            AuthorizationType = AuthorizationType.Username,
            Metadata = new RequestMetadata { RequestedTenantId = tenantId, IpAddress = "198.51.100.10" }
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Be("Invalid credentials");
    }

    private sealed class AllowingRateLimiter : IDistributedSecurityRateLimiter
    {
        public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
            StrictSecurityRateLimitPolicy policy,
            string clientKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(DistributedSecurityRateLimitDecision.Allowed);
    }
}
