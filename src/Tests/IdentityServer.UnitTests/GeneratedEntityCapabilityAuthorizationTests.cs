using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Middlewares;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class GeneratedEntityCapabilityAuthorizationTests
{
    [Test]
    public async Task FeatureGate_UsesGeneratedEndpointCapabilityInsteadOfHttpMethodInference()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var nextCalled = false;
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

        var middleware = new TenantModuleFeatureGateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            options,
            NullLogger<TenantModuleFeatureGateMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/widgets";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString("D")),
            new Claim("credential_id", credentialId.ToString("D"))
        ], "test"));
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new TenantCapabilityRequirement("update")),
            "generated update"));

        await middleware.InvokeAsync(
            context,
            featureService.Object,
            capabilityService.Object,
            new ConfigurationBuilder().Build());

        nextCalled.Should().BeTrue();
        capabilityService.VerifyAll();
        capabilityService.Verify(service => service.EnsureAllowedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            "create",
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
