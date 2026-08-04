using Communications.Integration.Drivers;
using FluentAssertions;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
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

[TestFixture]
public sealed class AuthServiceTrustedThrottlingTests
{
    [Test]
    public async Task BoltAuthenticationThrottles_UseValidatedCallerInsteadOfMetadataIp()
    {
        var limiter = new CapturingRateLimiter();
        var tenantId = Guid.NewGuid();
        var invocationContext = new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            Actor: null,
            Service: new TrustedServiceIdentity(
                "trusted-portal",
                XFrameworkServiceNames.IdentityServer,
                new HashSet<string>(),
                GenerationId: null),
            EffectiveTenantId: tenantId,
            RequestedTargetTenantId: tenantId,
            CorrelationId: Guid.NewGuid()));
        using var dbContext = new DbContext(new DbContextOptionsBuilder<DbContext>().Options);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(
            Mock.Of<IDataContext>(),
            dbContext,
            Mock.Of<IServiceScopeFactory>(),
            new HttpContextAccessor(),
            Mock.Of<ITenantResolver>(),
            Mock.Of<IJwtService>(),
            TimeProvider.System,
            limiter,
            invocationContext,
            new CacheManager(memoryCache, NullLogger<CacheManager>.Instance),
            Mock.Of<IStorageServiceWrapper>(),
            Mock.Of<IIdentityAuthorizationService>(),
            NullLogger<AuthService>.Instance);
        await service.AuthenticateAsync(CreateAuthenticateRequest(tenantId, "198.51.100.10"));
        await service.AuthenticateAsync(CreateAuthenticateRequest(tenantId, "203.0.113.10"));
        await service.ForgotPasswordAsync(CreateForgotPasswordRequest(tenantId, "198.51.100.11"));
        await service.ForgotPasswordAsync(CreateForgotPasswordRequest(tenantId, "203.0.113.11"));
        await service.ResetPasswordAsync(CreateResetPasswordRequest(tenantId, "198.51.100.12"));
        await service.ResetPasswordAsync(CreateResetPasswordRequest(tenantId, "203.0.113.12"));

        limiter.ClientKeys.Should().HaveCount(6);
        limiter.ClientKeys[0].Should().Be(limiter.ClientKeys[1]);
        limiter.ClientKeys[2].Should().Be(limiter.ClientKeys[3]);
        limiter.ClientKeys[4].Should().Be(limiter.ClientKeys[5]);
        limiter.ClientKeys[0].Should().Be(
            StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
                null,
                "service:trusted-portal:unknown-user"));
    }

    private static AuthenticateIdentityRequest CreateAuthenticateRequest(Guid tenantId, string ipAddress) => new()
    {
        UserName = "unknown-user",
        Password = "ValidPassword123!",
        RoleId = Guid.NewGuid(),
        AuthorizationType = AuthorizationType.Username,
        Metadata = CreateMetadata(tenantId, ipAddress)
    };

    private static ForgotPasswordRequest CreateForgotPasswordRequest(Guid tenantId, string ipAddress) => new()
    {
        Email = "unknown@example.test",
        Metadata = CreateMetadata(tenantId, ipAddress)
    };

    private static ResetPasswordRequest CreateResetPasswordRequest(Guid tenantId, string ipAddress) => new()
    {
        Token = "unknown-reset-token",
        NewPassword = "ValidPassword123!",
        Metadata = CreateMetadata(tenantId, ipAddress)
    };

    private static RequestMetadata CreateMetadata(Guid tenantId, string ipAddress) => new()
    {
        RequestedTenantId = tenantId,
        IpAddress = ipAddress
    };

    private sealed class CapturingRateLimiter : IDistributedSecurityRateLimiter
    {
        public List<string> ClientKeys { get; } = [];

        public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
            StrictSecurityRateLimitPolicy policy,
            string clientKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientKeys.Add(clientKey);
            return ValueTask.FromResult(
                DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromMinutes(1)));
        }
    }
}
