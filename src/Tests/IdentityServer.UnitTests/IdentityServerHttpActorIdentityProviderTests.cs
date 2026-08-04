using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
[Category("Module:IdentityServer")]
[Category("Area:SessionValidation")]
public sealed class IdentityServerHttpActorIdentityProviderTests
{
    [Test]
    public async Task ValidateAsync_ActiveSession_ReturnsTrustedIdentityAndForwardsBearerToken()
    {
        var snapshot = new ValidateIdentitySessionResponse
        {
            IsValid = true,
            TenantId = Guid.NewGuid(),
            CredentialId = Guid.NewGuid(),
            IdentityId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            GenerationId = "actor-generation",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            Roles = ["Admin"],
            Capabilities = ["identity.tenants:manage"]
        };
        HttpRequestMessage? observedRequest = null;
        var provider = CreateProvider(async (request, ct) =>
        {
            observedRequest = request;
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(snapshot)
            };
        });

        var result = await provider.ValidateAsync("actor-token");

        result.IsValid.Should().BeTrue(result.Error);
        result.Identity!.TenantId.Should().Be(snapshot.TenantId);
        result.Identity.CredentialId.Should().Be(snapshot.CredentialId);
        result.Identity.Capabilities.Should().Contain("identity.tenants:manage");
        observedRequest!.RequestUri!.AbsolutePath.Should().Be("/api/auth/validate-session");
        observedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        observedRequest.Headers.Authorization.Parameter.Should().Be("actor-token");
        observedRequest.Headers.GetValues("X-XFramework-Service-Authorization")
            .Should().ContainSingle().Which.Should().Be("Bearer service-token");
    }

    [Test]
    public async Task ValidateAsync_IdentityServerUnavailable_FailsClosed()
    {
        var provider = CreateProvider((_, _) => throw new HttpRequestException("network details"));

        var result = await provider.ValidateAsync("actor-token");

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Error.Should().Be("Actor identity validation is unavailable.");
    }

    [Test]
    public async Task ValidateAsync_RejectedToken_PreservesAuthenticationFailureStatus()
    {
        var provider = CreateProvider((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var result = await provider.ValidateAsync("actor-token");

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Error.Should().Be("Actor identity is invalid.");
    }

    [Test]
    public async Task ValidateAsync_SameToken_RevalidatesCurrentRolesAndSessionState()
    {
        var calls = 0;
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var provider = CreateProvider((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ValidateIdentitySessionResponse
                {
                    IsValid = true,
                    TenantId = tenantId,
                    CredentialId = credentialId,
                    IdentityId = identityId,
                    SessionId = sessionId,
                    GenerationId = "actor-generation",
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                    Roles = calls == 1 ? ["Admin"] : ["ReadOnly"]
                })
            });
        });

        var first = await provider.ValidateAsync("same-actor-token");
        var second = await provider.ValidateAsync("same-actor-token");

        calls.Should().Be(2, "session revocation and authorization changes must take effect on the next invocation");
        first.Identity!.Roles.Should().Contain("Admin");
        second.Identity!.Roles.Should().Contain("ReadOnly").And.NotContain("Admin");
    }

    private static IdentityServerHttpActorIdentityProvider CreateProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        var client = new HttpClient(new StubHandler(sendAsync));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(It.IsAny<string>())).Returns(client);
        var serviceTokenProvider = new Mock<IServiceTokenProvider>();
        serviceTokenProvider
            .Setup(value => value.GetTokenAsync(
                XFrameworkServiceNames.IdentityServer,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Contains(XFrameworkServiceScopes.IdentitySessionValidate)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("service-token");
        return new IdentityServerHttpActorIdentityProvider(
            factory.Object,
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.test",
                AllowInsecureHttp = true
            }),
            serviceTokenProvider.Object,
            Mock.Of<ILogger<IdentityServerHttpActorIdentityProvider>>());
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}
