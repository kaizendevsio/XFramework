using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.IdentityServer)]
public sealed class TrustedBackgroundServiceTargetTests
{
    [TestCase(XFrameworkServiceNames.IdentityServer, null)]
    [TestCase(XFrameworkServiceNames.Communications, XFrameworkServiceScopes.BoltService)]
    [TestCase(XFrameworkServiceNames.Storage, XFrameworkServiceScopes.StorageWrite)]
    public async Task EstablishAsync_WithIdentityServerServiceCredential_EstablishesTargetTenant(
        string audience,
        string? operationScope)
    {
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<ITrustedServiceTargetContextInitializer>();

        var result = await initializer.EstablishAsync(
            IntegrationTestFixture.TestTenantId,
            audience,
            operationScope is null ? [] : [operationScope],
            XFrameworkServiceNames.IdentityServer,
            Guid.NewGuid());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.Actor.Should().BeNull();
        result.Context.Service!.ClientId.Should().Be(XFrameworkServiceNames.IdentityServer);
        result.Context.Service.Audience.Should().Be(audience);
        result.Context.Service.Scopes.Should().Contain(XFrameworkServiceScopes.TenantTarget);
        result.Context.EffectiveTenantId.Should().Be(IntegrationTestFixture.TestTenantId);
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextAccessor>()
            .Current.Should().BeSameAs(result.Context);
    }

    [Test]
    public async Task EstablishAsync_WhenAllowedCallerDoesNotMatch_RejectsWithoutSettingContext()
    {
        await using var scope = IntegrationTestFixture.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<ITrustedServiceTargetContextInitializer>();

        var result = await initializer.EstablishAsync(
            IntegrationTestFixture.TestTenantId,
            XFrameworkServiceNames.Storage,
            [XFrameworkServiceScopes.StorageWrite],
            XFrameworkServiceNames.Portal,
            Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextAccessor>()
            .Current.Should().BeNull();
    }
}
