using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Bolt.Domain.Shared.Contracts.Requests;
using FluentAssertions;
using IdentityServer.Api.Features.ServiceIdentity.GetBoltTransportJwks;
using IdentityServer.Api.Features.ServiceIdentity.GetBoltTransportMetadata;
using IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;
using IdentityServer.Api.Features.ServiceIdentity.IssueToken;
using IdentityServer.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
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
    private const string Issuer = XFrameworkServiceNames.IdentityServer;

    private string _temporaryDirectory = null!;
    private string _signingKeyPath = null!;
    private IBoltTransportTokenSigner _signer = null!;

    [OneTimeSetUp]
    public void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "XFramework.IdentityServer.UnitTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _signingKeyPath = Path.Combine(_temporaryDirectory, "bolt-transport-signing-key.pem");
        var now = DateTimeOffset.Parse("2026-07-13T00:00:00Z");
        var configuration = ServiceIdentityConfiguration.FromConfiguration(CreateConfiguration(now), now);
        _signer = new FileBackedBoltTransportTokenSigner(configuration);
    }

    [OneTimeTearDown]
    public void DeleteTemporaryDirectory()
    {
        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [Test]
    public async Task IssueBoltTransportToken_DisabledByDefault_FailsClosed()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T01:00:00Z"));
        var fixture = CreateService(clock, enabled: null, includeSigningKeyPath: false);

        var result = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
        result.Message.Should().Be("Bolt transport token issuance is disabled");
    }

    [Test]
    public void Configuration_EnabledWithoutSigningKeyPath_FailsStartup()
    {
        var now = DateTimeOffset.Parse("2026-07-13T01:00:00Z");
        var configuration = CreateConfiguration(now, enabled: true, includeSigningKeyPath: false);

        var parse = () => ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parse.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKeyPath is required*");
    }

    [Test]
    public void Configuration_AllowInsecureHttp_DefaultsToFalseAndCanBeExplicitlyEnabled()
    {
        var now = DateTimeOffset.Parse("2026-07-13T01:00:00Z");
        var secureByDefault = ServiceIdentityConfiguration.FromConfiguration(
            CreateConfiguration(now, enabled: false, includeSigningKeyPath: false),
            now);
        var explicitlyInsecure = ServiceIdentityConfiguration.FromConfiguration(
            CreateConfiguration(
                now,
                enabled: false,
                allowInsecureHttp: true,
                includeSigningKeyPath: false),
            now);

        secureByDefault.AllowInsecureHttp.Should().BeFalse();
        explicitlyInsecure.AllowInsecureHttp.Should().BeTrue();
    }

    [Test]
    public async Task BoltTransportEndpoint_InsecureHttp_RejectsBeforeCredentialValidation()
    {
        var fixture = CreateService(
            new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T01:00:00Z")),
            enabled: false,
            includeSigningKeyPath: false);
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
            fixture.Configuration,
            service.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("HTTPS is required");
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ServiceTokenEndpoint_InsecureHttp_RejectsBeforeCredentialValidation()
    {
        var fixture = CreateService(
            new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T01:00:00Z")),
            enabled: false,
            includeSigningKeyPath: false);
        var request = new IssueServiceTokenRequest
        {
            ClientId = ClientId,
            ClientSecret = CurrentClientSecret,
            Audience = XFrameworkServiceNames.IdentityServer
        };
        var service = new Mock<IServiceIdentityService>(MockBehavior.Strict);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        var result = await IssueServiceTokenEndpoint.Handle(
            request,
            context.Request,
            fixture.Configuration,
            service.Object,
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("HTTPS is required");
        service.VerifyNoOtherCalls();
    }

    [Test]
    public async Task BoltTransportEndpoint_AllowInsecureHttp_DelegatesSubmittedCredentials()
    {
        var fixture = CreateService(
            new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T01:00:00Z")),
            enabled: false,
            allowInsecureHttp: true,
            includeSigningKeyPath: false);
        var expected = Result<ServiceTokenResponse>.Success(new ServiceTokenResponse());
        var service = new Mock<IServiceIdentityService>(MockBehavior.Strict);
        service.Setup(candidate => candidate.IssueBoltTransportTokenAsync(
                ClientId,
                CurrentClientSecret,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        var result = await IssueBoltTransportTokenEndpoint.Handle(
            new IssueBoltTransportTokenRequest
            {
                ClientId = ClientId,
                ClientSecret = CurrentClientSecret
            },
            context.Request,
            fixture.Configuration,
            service.Object,
            CancellationToken.None);

        result.Should().BeSameAs(expected);
        service.VerifyAll();
    }

    [Test]
    public void CredentialEndpoints_AreHttpOnlyAndExcludedFromOpenApi()
    {
        AssertCredentialEndpoint(
            typeof(IssueBoltTransportTokenEndpoint),
            BoltTransportTokenConstants.TokenEndpointPath);
        AssertCredentialEndpoint(
            typeof(IssueServiceTokenEndpoint),
            "/api/service-identity/token");
    }

    [Test]
    public void ServiceTokenCredentialRequest_IsNotBoltRoutable()
    {
        typeof(IssueServiceTokenRequest).GetInterfaces()
            .Should().NotContain(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == typeof(IBoltRequest<,>));
    }

    [Test]
    public async Task IssueBoltTransportToken_CurrentCredential_IssuesValidatedRs256TransportIdentity()
    {
        var now = DateTimeOffset.Parse("2026-07-13T01:02:03Z");
        var clock = new MutableTimeProvider(now);
        var fixture = CreateService(clock, lifetimeSeconds: 120);

        var result = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TokenType.Should().Be("Bearer");
        result.Data.ExpiresAtUtc.Should().Be(now.UtcDateTime.AddSeconds(120));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Data.AccessToken);
        token.Header.Alg.Should().Be(BoltTransportTokenConstants.Algorithm);
        token.Header.Typ.Should().Be(BoltTransportTokenConstants.TokenType);
        token.Header.Kid.Should().Be(fixture.Signer.KeyId);
        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().Equal(BoltTransportTokenConstants.Audience);
        Claim(token, "client_id").Should().Be(ClientId);
        Claim(token, "service").Should().Be(ClientId);
        Claim(token, JwtRegisteredClaimNames.Sub).Should().Be(ClientId);
        Claim(token, "scope").Should().Be(BoltTransportTokenConstants.Scope);
        Claim(token, "client_credential_generation").Should().Be(CurrentClientGeneration);
        token.Claims.Should().NotContain(claim => claim.Type == JwtCredentialSet.GenerationClaim);
        Claim(token, JwtRegisteredClaimNames.Jti).Should().NotBeNullOrWhiteSpace();
        Claim(token, JwtRegisteredClaimNames.Iat).Should().Be(now.ToUnixTimeSeconds().ToString());
        Claim(token, JwtRegisteredClaimNames.Nbf).Should().Be(now.ToUnixTimeSeconds().ToString());
        Claim(token, JwtRegisteredClaimNames.Exp).Should().Be(now.AddSeconds(120).ToUnixTimeSeconds().ToString());

        using var validationRsa = CreatePublicRsa(fixture.Signer.GetJsonWebKeySet().Keys.Single());
        var validate = () => new JwtSecurityTokenHandler().ValidateToken(
            result.Data.AccessToken,
            new TokenValidationParameters
            {
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new RsaSecurityKey(validationRsa)
                {
                    KeyId = fixture.Signer.KeyId,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                },
                RequireAudience = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false,
                ValidAlgorithms = [BoltTransportTokenConstants.Algorithm],
                ValidAudience = BoltTransportTokenConstants.Audience,
                ValidIssuer = Issuer,
                ValidTypes = [BoltTransportTokenConstants.TokenType]
            },
            out _);
        validate.Should().NotThrow();
    }

    [Test]
    public async Task IssueBoltTransportToken_SignerConcurrentCallers_RemainsThreadSafe()
    {
        const int callerCount = 16;
        var now = DateTimeOffset.Parse("2026-07-13T01:30:00Z");
        var fixture = CreateService(new MutableTimeProvider(now));
        var warmUpTokens = new List<string>();
        for (var index = 0; index < 4; index++)
        {
            var warmUp = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);
            warmUp.IsSuccess.Should().BeTrue();
            warmUp.Data.Should().NotBeNull();
            warmUpTokens.Add(warmUp.Data!.AccessToken);
        }

        using var ready = new CountdownEvent(callerCount);
        using var release = new ManualResetEventSlim();
        var calls = Enumerable.Range(0, callerCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    ready.Signal();
                    release.Wait();
                    return fixture.Service
                        .IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret)
                        .GetAwaiter()
                        .GetResult();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        ready.Wait();
        release.Set();
        var results = await Task.WhenAll(calls);
        var followUp = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, CurrentClientSecret);

        results.Should().OnlyContain(result => result.IsSuccess && result.Data != null);
        followUp.IsSuccess.Should().BeTrue();
        followUp.Data.Should().NotBeNull();
        var accessTokens = warmUpTokens
            .Concat(results.Select(result => result.Data!.AccessToken))
            .Append(followUp.Data!.AccessToken)
            .ToArray();
        accessTokens.Should().OnlyHaveUniqueItems();

        var tokenHandler = new JwtSecurityTokenHandler();
        var parsedTokens = accessTokens.Select(tokenHandler.ReadJwtToken).ToArray();
        parsedTokens.Select(token => Claim(token, JwtRegisteredClaimNames.Jti))
            .Should().OnlyHaveUniqueItems();
        parsedTokens.Should().OnlyContain(token => token.Header.Kid == fixture.Signer.KeyId);

        using var validationRsa = CreatePublicRsa(fixture.Signer.GetJsonWebKeySet().Keys.Single());
        var validationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new RsaSecurityKey(validationRsa)
            {
                KeyId = fixture.Signer.KeyId,
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            },
            RequireAudience = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidAlgorithms = [BoltTransportTokenConstants.Algorithm],
            ValidAudience = BoltTransportTokenConstants.Audience,
            ValidIssuer = Issuer,
            ValidTypes = [BoltTransportTokenConstants.TokenType]
        };
        for (var index = 0; index < accessTokens.Length; index++)
        {
            var validate = () => tokenHandler.ValidateToken(accessTokens[index], validationParameters, out _);
            validate.Should().NotThrow("token index {0} must remain valid after concurrent signing", index);
        }
    }

    [Test]
    public async Task IssueBoltTransportToken_FallbackCredential_UsesAuthenticatedCredentialGeneration()
    {
        var now = DateTimeOffset.Parse("2026-07-13T02:00:00Z");
        var fixture = CreateService(
            new MutableTimeProvider(now),
            fallbackValidUntilUtc: now.AddMinutes(5));

        var result = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        result.IsSuccess.Should().BeTrue();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Data!.AccessToken);
        token.Header.Kid.Should().Be(fixture.Signer.KeyId);
        Claim(token, "client_credential_generation").Should().Be(FallbackClientGeneration);
        Claim(token, "client_id").Should().Be(ClientId);
        Claim(token, "service").Should().Be(ClientId);
        Claim(token, JwtRegisteredClaimNames.Sub).Should().Be(ClientId);
        token.Claims.Should().NotContain(claim => claim.Type == JwtCredentialSet.GenerationClaim);
    }

    [Test]
    public async Task IssueBoltTransportToken_FallbackCredentialAtDeadline_IsRejected()
    {
        var now = DateTimeOffset.Parse("2026-07-13T03:00:00Z");
        var deadline = now.AddMinutes(5);
        var clock = new MutableTimeProvider(now);
        var fixture = CreateService(clock, fallbackValidUntilUtc: deadline);
        clock.SetUtcNow(deadline);

        var result = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Be("Invalid service client credentials");
    }

    [TestCase(59)]
    [TestCase(181)]
    public void Configuration_LifetimeOutsideBound_FailsStartup(int lifetimeSeconds)
    {
        var now = DateTimeOffset.Parse("2026-07-13T04:00:00Z");
        var configuration = CreateConfiguration(now, lifetimeSeconds: lifetimeSeconds);

        var parse = () => ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parse.Should().Throw<InvalidOperationException>()
            .WithMessage("*LifetimeSeconds must be between 60 and 180 seconds*");
    }

    [TestCase(60)]
    [TestCase(180)]
    public void Configuration_LifetimeAtBound_IsAccepted(int lifetimeSeconds)
    {
        var now = DateTimeOffset.Parse("2026-07-13T04:00:00Z");
        var configuration = CreateConfiguration(now, lifetimeSeconds: lifetimeSeconds);

        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parsed.BoltTransportTokenIssuerEnabled.Should().BeTrue();
        parsed.BoltTransportTokenLifetimeSeconds.Should().Be(lifetimeSeconds);
        parsed.BoltTransportSigningKeyPath.Should().Be(Path.GetFullPath(_signingKeyPath));
    }

    [Test]
    public void FileBackedSigner_ReloadsStablePublicKeyAndKeyId()
    {
        var now = DateTimeOffset.Parse("2026-07-13T04:30:00Z");
        var signingKeyPath = Path.Combine(_temporaryDirectory, $"reload-{Guid.NewGuid():N}.pem");
        var configuration = ServiceIdentityConfiguration.FromConfiguration(
            CreateConfiguration(now, signingKeyPath: signingKeyPath),
            now);

        var first = new FileBackedBoltTransportTokenSigner(configuration);
        var firstFile = File.ReadAllBytes(signingKeyPath);
        var second = new FileBackedBoltTransportTokenSigner(configuration);

        File.Exists(signingKeyPath).Should().BeTrue();
        File.ReadAllText(signingKeyPath).Should().Contain("BEGIN PRIVATE KEY");
        File.ReadAllBytes(signingKeyPath).Should().Equal(firstFile);
        second.KeyId.Should().Be(first.KeyId);
        second.GetJsonWebKeySet().Should().BeEquivalentTo(first.GetJsonWebKeySet());

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            File.GetUnixFileMode(signingKeyPath).Should().Be(
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public async Task JwksEndpoint_ReturnsOnlyRsa3072PublicMaterial()
    {
        var fixture = CreateService(new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T04:45:00Z")));

        var result = await GetBoltTransportJwksEndpoint.Handle(
            new GetBoltTransportJwksRequest(),
            fixture.Signer,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Keys.Should().ContainSingle();
        var key = result.Data.Keys.Single();
        key.KeyId.Should().Be(fixture.Signer.KeyId);
        key.Algorithm.Should().Be(BoltTransportTokenConstants.Algorithm);
        key.Use.Should().Be("sig");
        Base64UrlEncoder.DecodeBytes(key.Modulus).Should().HaveCount(384);

        var json = JsonSerializer.Serialize(result.Data);
        using var document = JsonDocument.Parse(json);
        var publicKeyProperties = document.RootElement
            .GetProperty("keys")[0]
            .EnumerateObject()
            .Select(static property => property.Name);
        publicKeyProperties.Should().BeEquivalentTo("kty", "use", "kid", "alg", "n", "e");
        json.Should().NotContain("PRIVATE KEY");
        json.Should().NotContain("\"d\"");
        json.Should().NotContain("\"p\"");
        json.Should().NotContain("\"q\"");
    }

    [Test]
    public async Task MetadataEndpoint_ReturnsJwtBearerDiscoveryDocument()
    {
        var fixture = CreateService(
            new MutableTimeProvider(DateTimeOffset.Parse("2026-07-13T04:50:00Z")),
            enabled: false,
            includeSigningKeyPath: false);
        var result = await GetBoltTransportMetadataEndpoint.Handle(
            new GetBoltTransportMetadataRequest(),
            fixture.Configuration,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Issuer.Should().Be(Issuer);
        result.Data.JsonWebKeySetUri.Should().Be(
            $"https://identity.test:8443{BoltTransportTokenConstants.JsonWebKeySetPath}");
        result.Data.TokenEndpoint.Should().Be(
            $"https://identity.test:8443{BoltTransportTokenConstants.TokenEndpointPath}");
        result.Data.SigningAlgorithms.Should().Equal(BoltTransportTokenConstants.Algorithm);
    }

    [Test]
    public async Task IssueBoltTransportToken_FailuresAndLogs_DoNotExposeSecretsOrTokens()
    {
        const string invalidSecret = "invalid-supplied-secret-that-must-never-be-logged";
        var now = DateTimeOffset.Parse("2026-07-13T05:00:00Z");
        var logger = new CapturingLogger<ServiceIdentityService>();
        var fixture = CreateService(new MutableTimeProvider(now), logger: logger);

        var denied = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, invalidSecret);
        var issued = await fixture.Service.IssueBoltTransportTokenAsync(ClientId, FallbackClientSecret);

        denied.IsSuccess.Should().BeFalse();
        issued.IsSuccess.Should().BeTrue();
        var sensitiveValues = new[]
        {
            CurrentClientSecret,
            FallbackClientSecret,
            invalidSecret,
            issued.Data!.AccessToken,
            File.ReadAllText(_signingKeyPath)
        };
        foreach (var value in sensitiveValues)
        {
            denied.Message.Should().NotContain(value);
            logger.Messages.Should().OnlyContain(message => !message.Contains(value, StringComparison.Ordinal));
        }

        logger.Messages.Should().ContainSingle(message =>
            message.Contains(ClientId, StringComparison.Ordinal)
            && message.Contains(FallbackClientGeneration, StringComparison.Ordinal)
            && message.Contains(fixture.Signer.KeyId, StringComparison.Ordinal));
    }

    private ServiceFixture CreateService(
        MutableTimeProvider clock,
        bool? enabled = true,
        int? lifetimeSeconds = 120,
        DateTimeOffset? fallbackValidUntilUtc = null,
        bool? allowInsecureHttp = null,
        bool includeSigningKeyPath = true,
        ILogger<ServiceIdentityService>? logger = null)
    {
        var configuration = CreateConfiguration(
            clock.GetUtcNow(),
            enabled,
            lifetimeSeconds,
            fallbackValidUntilUtc,
            allowInsecureHttp,
            includeSigningKeyPath);
        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, clock.GetUtcNow());
        var signer = includeSigningKeyPath
            ? _signer
            : new FileBackedBoltTransportTokenSigner(parsed);

        var service = new ServiceIdentityService(
            new Mock<IDataContext>(MockBehavior.Strict).Object,
            configuration,
            parsed,
            signer,
            clock,
            logger ?? Mock.Of<ILogger<ServiceIdentityService>>());

        return new ServiceFixture(service, parsed, signer);
    }

    private IConfiguration CreateConfiguration(
        DateTimeOffset now,
        bool? enabled = true,
        int? lifetimeSeconds = 120,
        DateTimeOffset? fallbackValidUntilUtc = null,
        bool? allowInsecureHttp = null,
        bool includeSigningKeyPath = true,
        string? signingKeyPath = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ServiceIdentity:Authority"] = "https://identity.test:8443",
            ["ServiceIdentity:Issuer"] = Issuer,
            ["ServiceIdentity:Clients:0:ClientId"] = ClientId,
            ["ServiceIdentity:Clients:0:GenerationId"] = CurrentClientGeneration,
            ["ServiceIdentity:Clients:0:ClientSecret"] = CurrentClientSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:GenerationId"] = FallbackClientGeneration,
            ["ServiceIdentity:Clients:0:ValidationFallback:ClientSecret"] = FallbackClientSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:ValidUntilUtc"] =
                (fallbackValidUntilUtc ?? now.AddMinutes(10)).ToString("O"),
            ["ServiceIdentity:Clients:0:AllowedScopes:0"] = XFrameworkServiceScopes.BoltService
        };

        if (enabled.HasValue)
            values["ServiceIdentity:BoltTransportTokenIssuer:Enabled"] = enabled.Value.ToString();
        if (lifetimeSeconds.HasValue)
            values["ServiceIdentity:BoltTransportTokenIssuer:LifetimeSeconds"] = lifetimeSeconds.Value.ToString();
        if (allowInsecureHttp.HasValue)
            values["ServiceIdentity:AllowInsecureHttp"] = allowInsecureHttp.Value.ToString();
        if (includeSigningKeyPath)
        {
            values["ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath"] =
                signingKeyPath ?? _signingKeyPath;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static void AssertCredentialEndpoint(Type endpointType, string route)
    {
        var method = endpointType.GetMethod("Handle", BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();
        var endpointMethod = method!;
        endpointMethod.GetCustomAttribute<BoltHandlerAttribute>().Should().BeNull();
        var mapPost = endpointMethod.GetCustomAttribute<MapPostAttribute>();
        mapPost.Should().NotBeNull();
        mapPost!.Route.Should().Be(route);
        mapPost.ExcludeFromOpenApi.Should().BeTrue();
    }

    private static RSA CreatePublicRsa(BoltTransportJsonWebKey key)
    {
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Base64UrlEncoder.DecodeBytes(key.Modulus),
            Exponent = Base64UrlEncoder.DecodeBytes(key.Exponent)
        });
        return rsa;
    }

    private static string Claim(JwtSecurityToken token, string claimType) =>
        token.Claims.Single(claim => claim.Type == claimType).Value;

    private sealed record ServiceFixture(
        ServiceIdentityService Service,
        ServiceIdentityConfiguration Configuration,
        IBoltTransportTokenSigner Signer);

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
