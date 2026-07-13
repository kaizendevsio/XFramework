using System.Security.Claims;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using XFramework.Integration.Abstractions;

namespace Bolt.Tests;

[TestFixture]
public sealed class CommunicationsBoltTopicAuthorizerTests
{
    [Test]
    public async Task UnknownNamespace_IsDeniedWithoutTokenOrDatabaseWork()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);

        var allowed = await authorizer.AuthorizeAsync(CreateContext(topic: "unknown.topic"));

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [TestCase("communications.tenant.not-a-guid.user.00000000000000000000000000000000")]
    [TestCase("communications.tenant.00000000000000000000000000000000.unknown")]
    [TestCase("Communications.tenant.00000000000000000000000000000000.presence")]
    public async Task MalformedCommunicationsTopic_IsDeniedWithoutTokenOrDatabaseWork(string topic)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);

        var allowed = await authorizer.AuthorizeAsync(CreateContext(topic));

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task MalformedSubscriberId_IsDeniedBeforeCredentialOrDatabaseWork()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.user.{credentialId:N}",
            durable: true,
            subscriberId: $"communications:{tenantId:N}:{credentialId:N}:extra");

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task OversizedTopic_IsDeniedWithoutTokenOrDatabaseWork()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var topic = $"communications.tenant.{Guid.NewGuid():N}.presence.{new string('x', 128)}";

        var allowed = await authorizer.AuthorizeAsync(CreateContext(topic));

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task OversizedSubscriberId_IsDeniedBeforeCredentialOrDatabaseWork()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.user.{credentialId:N}",
            durable: true,
            subscriberId: $"communications:{tenantId:N}:{credentialId:N}:device:{new string('x', 180)}:user");

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task TransientSubscriberDifferentFromRegisteredClient_IsDeniedBeforeCredentialOrDatabaseWork()
    {
        var tenantId = Guid.NewGuid();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.presence",
            subscriberId: "different-client");

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task TokenValidationException_IsContainedAsDenial()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var jwtService = Substitute.For<IJwtService>();
        jwtService.DecodeJwtToken("invalid-token")
            .Returns(Task.FromException<(ClaimsPrincipal, System.IdentityModel.Tokens.Jwt.JwtSecurityToken)>(
                new InvalidOperationException("token failure")));
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.user.{credentialId:N}",
            durable: true,
            subscriberId: $"communications:{tenantId:N}:{credentialId:N}:device:test:user",
            actorAccessToken: "invalid-token");

        var action = async () => await authorizer.AuthorizeAsync(context);

        (await action.Should().NotThrowAsync()).Which.Should().BeFalse();
        scopeFactory.DidNotReceive().CreateScope();
    }

    [Test]
    public async Task DatabaseScopeException_IsContainedAsDenial()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(_ => throw new InvalidOperationException("database unavailable"));
        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(scopeFactory, jwtService);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, credentialId.ToString("N"))],
            "Test"));
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.user.{credentialId:N}",
            durable: true,
            subscriberId: $"communications:{tenantId:N}:{credentialId:N}:device:test:user",
            user: principal);

        var action = async () => await authorizer.AuthorizeAsync(context);

        (await action.Should().NotThrowAsync()).Which.Should().BeFalse();
    }

    private static CommunicationsBoltTopicAuthorizer CreateAuthorizer(
        IServiceScopeFactory scopeFactory,
        IJwtService jwtService) =>
        new(scopeFactory, jwtService, NullLogger<CommunicationsBoltTopicAuthorizer>.Instance);

    private static BoltTopicAuthorizationContext CreateContext(
        string topic,
        bool durable = false,
        string? subscriberId = null,
        string? actorAccessToken = null,
        ClaimsPrincipal? user = null) =>
        new(
            BoltTopicOperation.Subscribe,
            topic,
            0,
            durable,
            subscriberId,
            actorAccessToken,
            "connection",
            "client",
            "Client",
            user);
}
