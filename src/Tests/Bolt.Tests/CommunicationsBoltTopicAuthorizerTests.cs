using System.Security.Claims;
using Bolt.Hub.Services;
using Bolt.Server;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;

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
    public async Task ActorProviderException_IsContainedAsDenial()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await using var provider = new ServiceCollection()
            .AddScoped<IActorIdentityProvider, ThrowingActorIdentityProvider>()
            .BuildServiceProvider();
        var authorizer = CreateAuthorizer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IJwtService>());
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.user.{credentialId:N}",
            durable: true,
            subscriberId: $"communications:{tenantId:N}:{credentialId:N}:device:test:user",
            actorAccessToken: "invalid-token");

        var action = async () => await authorizer.AuthorizeAsync(context);

        (await action.Should().NotThrowAsync()).Which.Should().BeFalse();
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

    [Test]
    public async Task PresenceSubscription_EnabledCredentialInTopicTenant_IsAllowed()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, credentialId.ToString("N")),
                new Claim("tenantId", tenantId.ToString("D"))
            ],
            "Test"));
        await using var provider = await CreateDatabaseProviderAsync(
            principal,
            new IdentityCredential
            {
                Id = credentialId,
                TenantId = tenantId,
                IdentityInfoId = Guid.NewGuid(),
                IsEnabled = true,
                IsDeleted = false
            });

        var jwtService = Substitute.For<IJwtService>();
        var authorizer = CreateAuthorizer(provider.GetRequiredService<IServiceScopeFactory>(), jwtService);
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.presence",
            subscriberId: "client",
            actorAccessToken: "actor-token",
            user: principal);

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeTrue();
        _ = jwtService.DidNotReceive().DecodeJwtToken(Arg.Any<string>());
    }

    [Test]
    public async Task PresenceSubscription_ActorCredentialWithServiceHttpPrincipal_IsAllowed()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var servicePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "XFramework.Proxy")],
            "Test"));
        var actorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, credentialId.ToString("N")),
                new Claim("tenantId", tenantId.ToString("D"))
            ],
            "Test"));
        await using var provider = await CreateDatabaseProviderAsync(
            actorPrincipal,
            new IdentityCredential
            {
                Id = credentialId,
                TenantId = tenantId,
                IdentityInfoId = Guid.NewGuid(),
                IsEnabled = true,
                IsDeleted = false
            });
        var authorizer = CreateAuthorizer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IJwtService>());
        var context = CreateContext(
            $"communications.tenant.{tenantId:N}.presence",
            subscriberId: "client",
            actorAccessToken: "actor-token",
            user: servicePrincipal);

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeTrue();
    }

    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public async Task PresenceSubscription_InvalidCredentialStateOrTenant_IsDenied(
        bool enabled,
        bool deleted,
        bool wrongTenant)
    {
        var topicTenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, credentialId.ToString("N")),
                new Claim("tenantId", topicTenantId.ToString("D"))
            ],
            "Test"));
        await using var provider = await CreateDatabaseProviderAsync(
            principal,
            new IdentityCredential
            {
                Id = credentialId,
                TenantId = wrongTenant ? Guid.NewGuid() : topicTenantId,
                IdentityInfoId = Guid.NewGuid(),
                IsEnabled = enabled,
                IsDeleted = deleted
            });
        var authorizer = CreateAuthorizer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IJwtService>());
        var context = CreateContext(
            $"communications.tenant.{topicTenantId:N}.presence",
            subscriberId: "client",
            user: principal);

        var allowed = await authorizer.AuthorizeAsync(context);

        allowed.Should().BeFalse();
    }

    private static async Task<ServiceProvider> CreateDatabaseProviderAsync(
        ClaimsPrincipal httpPrincipal,
        IdentityCredential credential)
    {
        var services = new ServiceCollection();
        var databaseName = $"bolt-topic-authorization-{Guid.NewGuid():N}";
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = httpPrincipal }
        });
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IEffectiveTenantContextAccessor>(
            new TrustedTenantContextAccessor(credential.TenantId));
        services.AddDbContext<DbContext, TestIdentityDbContext>((_, options) =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IActorIdentityProvider>(_ =>
        {
            var credentialId = Guid.Parse(httpPrincipal.FindFirstValue(ClaimTypes.Name)!);
            var claimedTenantId = Guid.Parse(httpPrincipal.FindFirstValue("tenantId")!);
            var result = credential.IsEnabled && !credential.IsDeleted && credential.TenantId == claimedTenantId
                ? ActorIdentityValidationResult.Success(CreateActor(credentialId, claimedTenantId))
                : ActorIdentityValidationResult.Failure("Actor identity is invalid.");
            return new StubActorIdentityProvider(result);
        });

        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        db.Set<IdentityCredential>().Add(credential);
        await db.SaveChangesAsync();
        return provider;
    }

    private sealed class TrustedTenantContextAccessor(Guid tenantId) : IEffectiveTenantContextAccessor
    {
        public bool HasTrustedInvocation => true;
        public Guid? EffectiveTenantId => tenantId;
    }

    private sealed class TestIdentityDbContext(
        DbContextOptions<TestIdentityDbContext> options,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IEffectiveTenantContextAccessor effectiveTenantContextAccessor)
        : XDbContext(options, httpContextAccessor, configuration, effectiveTenantContextAccessor)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var credential = modelBuilder.Entity<IdentityCredential>();
            credential.HasKey(item => item.Id);
            credential.Ignore(item => item.Tenant);
            credential.Ignore(item => item.AuthorizationLogs);
            credential.Ignore(item => item.IdentityContacts);
            credential.Ignore(item => item.IdentityFavorites);
            credential.Ignore(item => item.IdentityInfo);
            credential.Ignore(item => item.IdentityRoles);
            credential.Ignore(item => item.IdentityVerifications);
            credential.Ignore(item => item.SessionData);
            credential.Ignore(item => item.Subscriptions);
            base.OnModelCreating(modelBuilder);
        }
    }

    private static CommunicationsBoltTopicAuthorizer CreateAuthorizer(
        IServiceScopeFactory scopeFactory,
        IJwtService jwtService) =>
        new(scopeFactory, NullLogger<CommunicationsBoltTopicAuthorizer>.Instance);

    private static TrustedActorIdentity CreateActor(Guid credentialId, Guid tenantId) => new(
        credentialId,
        Guid.NewGuid(),
        tenantId,
        Guid.NewGuid(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "test-generation",
        DateTimeOffset.UtcNow.AddMinutes(5));

    private sealed class StubActorIdentityProvider(ActorIdentityValidationResult result) : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingActorIdentityProvider : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            throw new InvalidOperationException("provider failure");
    }

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
