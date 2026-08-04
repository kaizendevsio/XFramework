using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class GeneratedEntityCapabilityAuthorizationTests
{
    [Test]
    public async Task FeatureGate_NormalizesGeneratedRouteAndUsesTrustedActorCapability()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var options = new TenantModuleFeatureGateOptions()
            .RequireFeature("identity", "/api/widgets", "users");
        var featureService = new Mock<ITenantModuleFeatureService>();
        featureService
            .Setup(service => service.EnsureEnabledAsync(
                tenantId,
                "identity",
                "users",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var capabilityService = new Mock<ITenantCredentialCapabilityService>();
        capabilityService
            .Setup(service => service.EnsureAllowedAsync(
                tenantId,
                credentialId,
                "identity",
                "users",
                "update",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var contextAccessor = new Mock<ITrustedInvocationContextAccessor>();
        contextAccessor.SetupGet(accessor => accessor.Current).Returns(
            new TrustedInvocationContext(
                new TrustedActorIdentity(
                    credentialId,
                    Guid.NewGuid(),
                    tenantId,
                    Guid.NewGuid(),
                    new HashSet<string>(),
                    new HashSet<string>(),
                    "generation",
                    DateTimeOffset.UtcNow.AddMinutes(5)),
                null,
                tenantId,
                null,
                Guid.NewGuid()));
        var gate = new TrustedInvocationFeatureGate(
            options,
            featureService.Object,
            capabilityService.Object,
            contextAccessor.Object,
            NullLogger<TrustedInvocationFeatureGate>.Instance);

        var result = await gate.EnsureAllowedAsync(
            "api/widgets",
            "POST",
            "update");

        result.IsSuccess.Should().BeTrue();
        featureService.VerifyAll();
        capabilityService.VerifyAll();
    }

    [Test]
    public async Task FeatureGate_WithoutTrustedInvocation_FailsClosed()
    {
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        var capabilityService = new Mock<ITenantCredentialCapabilityService>(MockBehavior.Strict);
        var contextAccessor = new Mock<ITrustedInvocationContextAccessor>();
        var gate = new TrustedInvocationFeatureGate(
            new TenantModuleFeatureGateOptions().RequireFeature("identity", "/api/widgets", "users"),
            featureService.Object,
            capabilityService.Object,
            contextAccessor.Object,
            NullLogger<TrustedInvocationFeatureGate>.Instance);

        var result = await gate.EnsureAllowedAsync(
            "/api/widgets",
            "GET",
            "view");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        featureService.VerifyNoOtherCalls();
        capabilityService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task FeatureGate_ForAuthorizedCrossTenantDelegation_DoesNotQueryTargetTenantCredential()
    {
        var actorTenantId = Guid.NewGuid();
        var targetTenantId = Guid.NewGuid();
        var options = new TenantModuleFeatureGateOptions()
            .RequireFeature("identity", "/api/widgets", "users");
        var featureService = new Mock<ITenantModuleFeatureService>();
        featureService
            .Setup(service => service.EnsureEnabledAsync(
                targetTenantId,
                "identity",
                "users",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var capabilityService = new Mock<ITenantCredentialCapabilityService>(MockBehavior.Strict);
        var contextAccessor = new Mock<ITrustedInvocationContextAccessor>();
        contextAccessor.SetupGet(accessor => accessor.Current).Returns(
            new TrustedInvocationContext(
                new TrustedActorIdentity(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    actorTenantId,
                    Guid.NewGuid(),
                    new HashSet<string>(),
                    new HashSet<string>(["identity.tenants:manage"]),
                    "generation",
                    DateTimeOffset.UtcNow.AddMinutes(5)),
                null,
                targetTenantId,
                targetTenantId,
                Guid.NewGuid()));
        var gate = new TrustedInvocationFeatureGate(
            options,
            featureService.Object,
            capabilityService.Object,
            contextAccessor.Object,
            NullLogger<TrustedInvocationFeatureGate>.Instance);

        var result = await gate.EnsureAllowedAsync("/api/widgets", "POST", "update");

        result.IsSuccess.Should().BeTrue();
        featureService.VerifyAll();
        capabilityService.VerifyNoOtherCalls();
    }
}
