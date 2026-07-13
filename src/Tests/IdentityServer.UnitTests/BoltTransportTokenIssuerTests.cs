using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using FluentAssertions;
using IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class BoltTransportTokenIssuerTests
{
    private const string ClientId = XFrameworkServiceNames.Communications;
    private const string CurrentClientGeneration = "service-g1";
    private const string FallbackClientGeneration = "service-g0";
    private const string CurrentClientSecret = "current-service-credential-material-1111111111111111111111111111";
    private const string FallbackClientSecret = "fallback-service-credential-material-0000000000000000000000000000";
    private const string CurrentJwtGeneration = "jwt-g1";
    private const string FallbackJwtGeneration = "jwt-g0";
    private const string CurrentJwtSecret = "current-jwt-signing-material-111111111111111111111111111111111111111111111111";
    private const string FallbackJwtSecret = "fallback-jwt-signing-material-000000000000000000000000000000000000000000000000";
    private const string Issuer = "https://identity.test";
    private const string Audience = "https://bolt.test";

    [Test]
    public async Task IssueBoltTransportToken_DisabledByDefault_FailsClosed()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T01:00:00Z"));
        var service = CreateService(clock, enabled: null);

        var result = await service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Message.Should().Be("Bolt transport token issuance is disabled");
    }

    [Test]
    public async Task Endpoint_InsecureHttp_RejectsBeforeCredentialValidation()
    {
        var service = new Mock<IServiceIdentityService>(MockBehavior.Strict);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        var result = await IssueBoltTransportTokenEndpoint.Handle(
            new IssueBoltTransportTokenRequest
            {
                ClientId = ClientId,
                ClientSecret = CurrentClientSecret
            },
            context.Request,
            service.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("HTTPS is required");
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Endpoint_Https_DelegatesOnlySubmittedClientCredentials()
    {
        var expected = Result<ServiceTokenResponse>.Success(new ServiceTokenResponse());
        var service = new Mock<IServiceIdentityService>(MockBehavior.Strict);
        service.Setup(candidate => candidate.IssueBoltTransportTokenAsync(
                ClientId,
                CurrentClientSecret,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        var result = await IssueBoltTransportTokenEndpoint.Handle(
            new IssueBoltTransportTokenRequest
            {
                ClientId = ClientId,
                ClientSecret = CurrentClientSecret
            },
            context.Request,
            service.Object,
            CancellationToken.None);

        result.Should().BeSameAs(expected);
        service.VerifyAll();
    }

    [Test]
    public void Endpoint_IsHttpOnlyAndExcludedFromOpenApi()
    {
        var method = typeof(IssueBoltTransportTokenEndpoint).GetMethod(
            nameof(IssueBoltTransportTokenEndpoint.Handle),
            BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();
        var endpointMethod = method!;
        endpointMethod.GetCustomAttribute<BoltHandlerAttribute>().Should().BeNull();
        var mapPost = endpointMethod.GetCustomAttribute<MapPostAttribute>();
        mapPost.Should().NotBeNull();
        mapPost!.Route.Should().Be("/api/service-identity/bolt-transport-token");
        mapPost.ExcludeFromOpenApi.Should().BeTrue();
    }

    [Test]
    public async Task IssueBoltTransportToken_CurrentCredential_IssuesExactCurrentGenerationIdentityAndClaims()
    {
        var now = DateTimeOffset.Parse("2026-07-13T01:02:03Z");
        var clock = new MutableTimeProvider(now);
        var jwtOptions = CreateJwtOptions(now);
        var service = CreateService(clock, jwtOptions: jwtOptions, lifetimeSeconds: 120);

        var result = await service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TokenType.Should().Be("Bearer");
        result.Data.ExpiresAtUtc.Should().Be(now.UtcDateTime.AddSeconds(120));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Data.AccessToken);
        token.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha512);
        token.Header.Kid.Should().Be(CurrentJwtGeneration);
        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().Equal(Audience);
        Claim(token, "client_id").Should().Be(ClientId);
        Claim(token, "service").Should().Be(ClientId);
        Claim(token, JwtRegisteredClaimNames.Sub).Should().Be(ClientId);
        Claim(token, "scope").Should().Be(XFrameworkServiceScopes.BoltService);
        Claim(token, JwtCredentialSet.GenerationClaim).Should().Be(CurrentJwtGeneration);
        Claim(token, "client_credential_generation").Should().Be(CurrentClientGeneration);
        Claim(token, JwtRegisteredClaimNames.Jti).Should().NotBeNullOrWhiteSpace();
        Claim(token, JwtRegisteredClaimNames.Iat).Should().Be(now.ToUnixTimeSeconds().ToString());
        Claim(token, JwtRegisteredClaimNames.Nbf).Should().Be(now.ToUnixTimeSeconds().ToString());
        Claim(token, JwtRegisteredClaimNames.Exp).Should().Be(now.AddSeconds(120).ToUnixTimeSeconds().ToString());

        var validate = () => new JwtSecurityTokenHandler().ValidateToken(
            result.Data.AccessToken,
            JwtCredentialSet.CreateValidationParameters(jwtOptions, validateLifetime: false, clock),
            out _);
        validate.Should().NotThrow();
    }

    [Test]
    public async Task IssueBoltTransportToken_FallbackClientCredentialBeforeDeadline_StillUsesCurrentJwtGeneration()
    {
        var now = DateTimeOffset.Parse("2026-07-13T02:00:00Z");
        var clock = new MutableTimeProvider(now);
        var service = CreateService(clock, fallbackValidUntilUtc: now.AddMinutes(5));

        var result = await service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        result.IsSuccess.Should().BeTrue();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Data!.AccessToken);
        token.Header.Kid.Should().Be(CurrentJwtGeneration);
        Claim(token, JwtCredentialSet.GenerationClaim).Should().Be(CurrentJwtGeneration);
        Claim(token, "client_credential_generation").Should().Be(FallbackClientGeneration);
        Claim(token, "client_id").Should().Be(ClientId);
        Claim(token, "service").Should().Be(ClientId);
        Claim(token, JwtRegisteredClaimNames.Sub).Should().Be(ClientId);
    }

    [Test]
    public async Task IssueBoltTransportToken_FallbackClientCredentialAtDeadline_IsRejected()
    {
        var now = DateTimeOffset.Parse("2026-07-13T03:00:00Z");
        var deadline = now.AddMinutes(5);
        var clock = new MutableTimeProvider(now);
        var service = CreateService(clock, fallbackValidUntilUtc: deadline);
        clock.SetUtcNow(deadline);

        var result = await service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Be("Invalid service client credentials");
    }

    [TestCase(59)]
    [TestCase(181)]
    public void Configuration_LifetimeOutsideBound_FailsStartup(int lifetimeSeconds)
    {
        var now = DateTimeOffset.Parse("2026-07-13T04:00:00Z");
        var configuration = CreateConfiguration(now, enabled: true, lifetimeSeconds: lifetimeSeconds);

        var parse = () => ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parse.Should().Throw<InvalidOperationException>()
            .WithMessage("*LifetimeSeconds must be between 60 and 180 seconds*");
    }

    [TestCase(60)]
    [TestCase(180)]
    public void Configuration_LifetimeAtBound_IsAccepted(int lifetimeSeconds)
    {
        var now = DateTimeOffset.Parse("2026-07-13T04:00:00Z");
        var configuration = CreateConfiguration(now, enabled: true, lifetimeSeconds: lifetimeSeconds);

        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parsed.BoltTransportTokenIssuerEnabled.Should().BeTrue();
        parsed.BoltTransportTokenLifetimeSeconds.Should().Be(lifetimeSeconds);
    }

    [Test]
    public async Task IssueBoltTransportToken_FailuresAndLogs_DoNotExposeSecretsOrTokens()
    {
        const string invalidSecret = "invalid-supplied-secret-that-must-never-be-logged";
        var now = DateTimeOffset.Parse("2026-07-13T05:00:00Z");
        var clock = new MutableTimeProvider(now);
        var logger = new CapturingLogger<ServiceIdentityService>();
        var service = CreateService(clock, logger: logger);

        var denied = await service.IssueBoltTransportTokenAsync(ClientId, invalidSecret);
        var issued = await service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        denied.IsSuccess.Should().BeFalse();
        issued.IsSuccess.Should().BeTrue();
        var sensitiveValues = new[]
        {
            CurrentClientSecret,
            FallbackClientSecret,
            CurrentJwtSecret,
            FallbackJwtSecret,
            invalidSecret,
            issued.Data!.AccessToken
        };
        foreach (var value in sensitiveValues)
        {
            denied.Message.Should().NotContain(value);
            logger.Messages.Should().OnlyContain(message => !message.Contains(value, StringComparison.Ordinal));
        }

        logger.Messages.Should().ContainSingle(message =>
            message.Contains(ClientId, StringComparison.Ordinal)
            && message.Contains(FallbackClientGeneration, StringComparison.Ordinal)
            && message.Contains(CurrentJwtGeneration, StringComparison.Ordinal));
    }

    private static ServiceIdentityService CreateService(
        MutableTimeProvider clock,
        bool? enabled = true,
        int? lifetimeSeconds = 120,
        DateTimeOffset? fallbackValidUntilUtc = null,
        JwtOptions? jwtOptions = null,
        ILogger<ServiceIdentityService>? logger = null)
    {
        var configuration = CreateConfiguration(
            clock.GetUtcNow(),
            enabled,
            lifetimeSeconds,
            fallbackValidUntilUtc);
        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, clock.GetUtcNow());

        return new ServiceIdentityService(
            new Mock<IDataContext>(MockBehavior.Strict).Object,
            configuration,
            parsed,
            jwtOptions ?? CreateJwtOptions(clock.GetUtcNow()),
            clock,
            logger ?? Mock.Of<ILogger<ServiceIdentityService>>());
    }

    private static IConfiguration CreateConfiguration(
        DateTimeOffset now,
        bool? enabled,
        int? lifetimeSeconds,
        DateTimeOffset? fallbackValidUntilUtc = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ServiceIdentity:Clients:0:ClientId"] = ClientId,
            ["ServiceIdentity:Clients:0:GenerationId"] = CurrentClientGeneration,
            ["ServiceIdentity:Clients:0:ClientSecret"] = CurrentClientSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:GenerationId"] = FallbackClientGeneration,
            ["ServiceIdentity:Clients:0:ValidationFallback:ClientSecret"] = FallbackClientSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:ValidUntilUtc"] =
                (fallbackValidUntilUtc ?? now.AddMinutes(10)).ToString("O"),
            ["ServiceIdentity:Clients:0:AllowedScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["ServiceIdentity:BoltTransportTokenIssuer:Enabled"] = enabled?.ToString(),
            ["ServiceIdentity:BoltTransportTokenIssuer:LifetimeSeconds"] = lifetimeSeconds?.ToString()
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static JwtOptions CreateJwtOptions(DateTimeOffset now) => new()
    {
        GenerationId = CurrentJwtGeneration,
        Secret = CurrentJwtSecret,
        ValidationFallback = new JwtValidationFallbackOptions
        {
            GenerationId = FallbackJwtGeneration,
            Secret = FallbackJwtSecret,
            ValidUntilUtc = now.AddMinutes(10)
        },
        ValidIssuer = Issuer,
        ValidAudience = Audience
    };

    private static string Claim(JwtSecurityToken token, string claimType) =>
        token.Claims.Single(claim => claim.Type == claimType).Value;

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void SetUtcNow(DateTimeOffset value) => utcNow = value;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
