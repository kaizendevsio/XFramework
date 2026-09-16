using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace Notifications.Tests.Services.Push;

public sealed class NotificationPushAuthorizationTests
{
    [TestCase("/api/notifications/push/configuration", "GET", "view")]
    [TestCase("/api/notifications/push/subscriptions", "POST", "create")]
    [TestCase("/api/notifications/push/subscriptions/remove", "POST", "delete")]
    public async Task FeatureGate_PushMemberWithoutModulePermissions_CanManageSubscriptions(
        string route, string method, string capability)
    {
        var fixture = CreateGate(capability);

        var result = await fixture.Gate.EnsureAllowedAsync(route, method, null);

        result.IsSuccess.Should().BeTrue();
        fixture.Capabilities.Verify(x => x.EnsureAllowedAsync(
            fixture.TenantId, fixture.CredentialId, "notifications", "push", capability,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Capabilities.VerifyNoOtherCalls();
    }

    [TestCase("/api/notifications", "POST")]
    [TestCase("/api/notifications/inbox", "GET")]
    [TestCase("/api/notifications/push/send", "POST")]
    [TestCase("/api/notifications/push/subscriptions-admin", "POST")]
    public async Task FeatureGate_PushMember_CannotAccessOtherNotificationRoutes(string route, string method)
    {
        var fixture = CreateGate("create");

        var result = await fixture.Gate.EnsureAllowedAsync(route, method, null);

        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task FeatureGate_MissingPushCreatePermission_RejectsRegistration()
    {
        var fixture = CreateGate("view");

        var result = await fixture.Gate.EnsureAllowedAsync(
            "/api/notifications/push/subscriptions", "POST", null);

        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task FeatureGate_DisabledPushFeature_RejectsBeforeCheckingPermissions()
    {
        var fixture = CreateGate("create", featureEnabled: false);

        var result = await fixture.Gate.EnsureAllowedAsync(
            "/api/notifications/push/subscriptions", "POST", null);

        result.StatusCode.Should().Be(403);
        fixture.Capabilities.VerifyNoOtherCalls();
    }

    private static GateFixture CreateGate(string grantedCapability, bool featureEnabled = true)
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var options = new TenantModuleFeatureGateOptions();
        Program.ConfigureFeatureGates(options);
        var features = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        features.Setup(x => x.EnsureEnabledAsync(
                tenantId, "notifications", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureEnabled ? Result.Success() : Result.Forbidden("Feature disabled"));
        var capabilities = new Mock<ITenantCredentialCapabilityService>(MockBehavior.Strict);
        capabilities.Setup(x => x.EnsureAllowedAsync(
                tenantId, credentialId, "notifications", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, string _, string? subFeature, string capability, CancellationToken _) =>
                subFeature == "push" && capability == grantedCapability
                    ? Result.Success()
                    : Result.Forbidden("Capability denied"));
        var gate = new TrustedInvocationFeatureGate(
            options, features.Object, capabilities.Object,
            new TestInvocationContextAccessor(tenantId, credentialId),
            NullLogger<TrustedInvocationFeatureGate>.Instance);
        return new GateFixture(gate, capabilities, tenantId, credentialId);
    }

    private sealed record GateFixture(
        TrustedInvocationFeatureGate Gate,
        Mock<ITenantCredentialCapabilityService> Capabilities,
        Guid TenantId,
        Guid CredentialId);
}
