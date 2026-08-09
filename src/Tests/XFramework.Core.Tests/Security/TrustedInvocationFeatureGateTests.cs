using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class TrustedInvocationFeatureGateTests
{
    [Test]
    public async Task GeneratedEntity_WithoutFeature_DuringCompatibilityPeriod_SkipsFeatureLookup()
    {
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        var gate = CreateGate(featureService);

        var result = await gate.EnsureGeneratedEntityAllowedAsync("", "view", requiresTenant: true);

        result.IsSuccess.Should().BeTrue();
        featureService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GeneratedTenantlessEntity_SkipsTenantFeatureLookup()
    {
        var featureService = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        var gate = CreateGate(featureService);

        var result = await gate.EnsureGeneratedEntityAllowedAsync(
            "wallets.reporting",
            "view",
            requiresTenant: false);

        result.IsSuccess.Should().BeTrue();
        featureService.VerifyNoOtherCalls();
    }

    private static TrustedInvocationFeatureGate CreateGate(
        Mock<ITenantModuleFeatureService> featureService) =>
        new(
            new TenantModuleFeatureGateOptions(),
            featureService.Object,
            Mock.Of<ITenantCredentialCapabilityService>(),
            Mock.Of<ITrustedInvocationContextAccessor>(),
            NullLogger<TrustedInvocationFeatureGate>.Instance);
}
