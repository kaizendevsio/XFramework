using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Cryptography;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ServiceIdentityCredentialGenerationTests
{
    private const string G0Secret = "service-g0-credential-material-000000000000000";
    private const string G1Secret = "service-g1-credential-material-111111111111111";

    [Test]
    public async Task IssueToken_G1PreStagedAsFallback_IssuesTokensMarkedWithAuthenticatedGeneration()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService("g0", G0Secret, "g1", G1Secret, clock.GetUtcNow().AddMinutes(10), clock);

        var currentResult = await service.IssueTokenAsync(CreateTokenRequest(G0Secret));
        var stagedResult = await service.IssueTokenAsync(CreateTokenRequest(G1Secret));

        currentResult.IsSuccess.Should().BeTrue();
        stagedResult.IsSuccess.Should().BeTrue();
        ReadCredentialGeneration(currentResult.Data!.AccessToken).Should().Be("g0");
        ReadCredentialGeneration(stagedResult.Data!.AccessToken).Should().Be("g1");
    }

    [Test]
    public async Task IssueToken_G1CurrentWithG0Fallback_RetiresG0AtDeadline()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5), clock);

        var beforeRetirement = await service.IssueTokenAsync(CreateTokenRequest(G0Secret));
        clock.Advance(TimeSpan.FromMinutes(6));
        var afterRetirement = await service.IssueTokenAsync(CreateTokenRequest(G0Secret));
        var currentAfterRetirement = await service.IssueTokenAsync(CreateTokenRequest(G1Secret));

        beforeRetirement.IsSuccess.Should().BeTrue();
        ReadCredentialGeneration(beforeRetirement.Data!.AccessToken).Should().Be("g0");
        afterRetirement.StatusCode.Should().Be(401);
        currentAfterRetirement.IsSuccess.Should().BeTrue();
        ReadCredentialGeneration(currentAfterRetirement.Data!.AccessToken).Should().Be("g1");
    }

    [Test]
    public void Configuration_MissingDuplicateExpiredEqualOrUnboundedGeneration_FailsStartupParsing()
    {
        var now = DateTimeOffset.UtcNow;
        var invalidConfigurations = new[]
        {
            CreateConfiguration("", G0Secret, "g1", G1Secret, now.AddMinutes(5)),
            CreateConfiguration("g0", G0Secret, "", G1Secret, now.AddMinutes(5)),
            CreateConfiguration("g0", G0Secret, "g0", G1Secret, now.AddMinutes(5)),
            CreateConfiguration("g0", G0Secret, "g1", G0Secret, now.AddMinutes(5)),
            CreateConfiguration("g0", G0Secret, "g1", G1Secret, null),
            CreateConfiguration("g0", G0Secret, "g1", G1Secret, now)
        };

        foreach (var configuration in invalidConfigurations)
        {
            var parse = () => ServiceIdentityConfiguration.FromConfiguration(configuration, now);
            parse.Should().Throw<InvalidOperationException>();
        }
    }

    [Test]
    public void ProviderRequest_G1PreStagedAsFallback_SendsOnlyCurrentG0Secret()
    {
        var now = DateTimeOffset.UtcNow;
        var options = new ServiceIdentityOptions
        {
            ClientId = "test-client",
            GenerationId = "g0",
            ClientSecret = G0Secret,
            ValidationFallback = new ServiceIdentityValidationFallbackOptions
            {
                GenerationId = "g1",
                ClientSecret = G1Secret,
                ValidUntilUtc = now.AddMinutes(10)
            }
        };

        var request = IdentityServerServiceTokenProvider.CreateCurrentCredentialRequest(
            options,
            "test-client",
            "test-audience",
            ["test.scope"],
            now);

        request.ClientSecret.Should().Be(G0Secret);
        request.ClientSecret.Should().NotBe(G1Secret);
    }

    [Test]
    public void Configuration_AllEmptyFallbackEnvironmentKeys_AreTreatedAsNotConfigured()
    {
        var now = DateTimeOffset.UtcNow;
        var configuration = CreateConfiguration("g0", G0Secret, "", "", null);

        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, now);

        parsed.ValidationGenerationIdsByClient["test-client"].Should().Equal("g0");
    }

    [Test]
    public void Configuration_NoServiceClients_FailsClosed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var parse = () => ServiceIdentityConfiguration.FromConfiguration(configuration, DateTimeOffset.UtcNow);

        parse.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one service client*");
    }

    [TestCase(false, true, "AllowedAudiences")]
    [TestCase(true, false, "AllowedScopes")]
    public void Configuration_MissingAllowedAudiencesOrScopes_FailsClosed(
        bool includeAllowedAudiences,
        bool includeAllowedScopes,
        string missingSetting)
    {
        var configuration = CreateConfiguration(
            "g0",
            G0Secret,
            "",
            "",
            null,
            includeAllowedAudiences,
            includeAllowedScopes);

        var parse = () => ServiceIdentityConfiguration.FromConfiguration(
            configuration,
            DateTimeOffset.UtcNow);

        parse.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{missingSetting}*");
    }

    [Test]
    public void ClientOptions_MissingDefaultScopes_FailsValidation()
    {
        var options = new ServiceIdentityOptions
        {
            Authority = "https://identity.example.test",
            ClientId = "test-client",
            GenerationId = "g0",
            ClientSecret = G0Secret,
            DefaultScopes = []
        };
        var validator = new ServiceIdentityOptionsValidator(TimeProvider.System);

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultScopes");
    }

    [Test]
    public void ProviderRequest_FallbackDeadlineHasPassed_StillSendsCurrentSecret()
    {
        var now = DateTimeOffset.UtcNow;
        var options = new ServiceIdentityOptions
        {
            ClientId = "test-client",
            GenerationId = "g1",
            ClientSecret = G1Secret,
            ValidationFallback = new ServiceIdentityValidationFallbackOptions
            {
                GenerationId = "g0",
                ClientSecret = G0Secret,
                ValidUntilUtc = now.AddMinutes(-1)
            }
        };

        var request = IdentityServerServiceTokenProvider.CreateCurrentCredentialRequest(
            options,
            "test-client",
            "test-audience",
            ["test.scope"],
            now);

        request.ClientSecret.Should().Be(G1Secret);
    }

    [Test]
    public async Task CredentialFailuresAndIssuanceLogs_DoNotExposeSecrets()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var logger = new CapturingLogger<ServiceIdentityService>();
        var service = CreateService(
            "g0",
            G0Secret,
            "g1",
            G1Secret,
            clock.GetUtcNow().AddMinutes(10),
            clock,
            logger);

        var issued = await service.IssueTokenAsync(CreateTokenRequest(G1Secret));
        var denied = await service.IssueTokenAsync(CreateTokenRequest("invalid-supplied-secret"));
        var invalidConfiguration = CreateConfiguration("g0", G0Secret, "g1", G0Secret, clock.GetUtcNow().AddMinutes(10));
        var parse = () => ServiceIdentityConfiguration.FromConfiguration(invalidConfiguration, clock.GetUtcNow());

        issued.IsSuccess.Should().BeTrue();
        denied.StatusCode.Should().Be(401);
        parse.Should().Throw<InvalidOperationException>()
            .Where(exception => !exception.Message.Contains(G0Secret, StringComparison.Ordinal)
                                && !exception.Message.Contains(G1Secret, StringComparison.Ordinal));
        logger.Messages.Should().OnlyContain(message =>
            !message.Contains(G0Secret, StringComparison.Ordinal)
            && !message.Contains(G1Secret, StringComparison.Ordinal));
    }

    private static ServiceIdentityService CreateService(
        string currentGenerationId,
        string currentSecret,
        string fallbackGenerationId,
        string fallbackSecret,
        DateTimeOffset validUntilUtc,
        TimeProvider clock,
        ILogger<ServiceIdentityService>? logger = null)
    {
        var configuration = CreateConfiguration(
            currentGenerationId,
            currentSecret,
            fallbackGenerationId,
            fallbackSecret,
            validUntilUtc);
        var parsed = ServiceIdentityConfiguration.FromConfiguration(configuration, clock.GetUtcNow());
        using var rsa = RSA.Create(2048);
        var signingKeyDirectory = configuration["ServiceIdentity:ServiceTokenSigningKeyDirectory"]!;
        Directory.CreateDirectory(signingKeyDirectory);
        const string signingKeyFileName = "test-signing-key.pem";
        File.WriteAllText(
            Path.Combine(signingKeyDirectory, signingKeyFileName),
            rsa.ExportPkcs8PrivateKeyPem());
        var signingKey = new ServiceSigningKey
        {
            Id = Guid.NewGuid(),
            KeyId = "test-signing-key",
            Algorithm = "RS256",
            PrivateKeyFileName = signingKeyFileName,
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            ActivatedAtUtc = clock.GetUtcNow().UtcDateTime,
            IsActive = true
        };
        var query = new Mock<IRemoteQuery<ServiceSigningKey>>(MockBehavior.Strict);
        query.Setup(remoteQuery => remoteQuery.Where(It.IsAny<Expression<Func<ServiceSigningKey, bool>>>()))
            .Returns(query.Object);
        query.Setup(remoteQuery => remoteQuery.FirstOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(signingKey);
        var dataContext = new Mock<IDataContext>(MockBehavior.Strict);
        dataContext.Setup(context => context.Query<ServiceSigningKey>()).Returns(query.Object);

        return new ServiceIdentityService(
            dataContext.Object,
            configuration,
            parsed,
            Mock.Of<IBoltTransportTokenSigner>(),
            clock,
            logger ?? NullLogger<ServiceIdentityService>.Instance);
    }

    private static IConfiguration CreateConfiguration(
        string currentGenerationId,
        string currentSecret,
        string fallbackGenerationId,
        string fallbackSecret,
        DateTimeOffset? validUntilUtc,
        bool includeAllowedAudiences = true,
        bool includeAllowedScopes = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["ServiceIdentity:Clients:0:ClientId"] = "test-client",
            ["ServiceIdentity:Clients:0:GenerationId"] = currentGenerationId,
            ["ServiceIdentity:Clients:0:ClientSecret"] = currentSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:GenerationId"] = fallbackGenerationId,
            ["ServiceIdentity:Clients:0:ValidationFallback:ClientSecret"] = fallbackSecret,
            ["ServiceIdentity:Clients:0:ValidationFallback:ValidUntilUtc"] = validUntilUtc?.ToString("O"),
            ["ServiceIdentity:ServiceTokenSigningKeyDirectory"] = Path.Combine(
                Path.GetTempPath(),
                "xframework-identity-unit-keys",
                Guid.NewGuid().ToString("N"))
        };

        if (includeAllowedAudiences)
        {
            values["ServiceIdentity:Clients:0:AllowedAudiences:0"] =
                XFrameworkServiceNames.Communications;
        }

        if (includeAllowedScopes)
        {
            values["ServiceIdentity:Clients:0:AllowedScopes:0"] =
                XFrameworkServiceScopes.BoltService;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IssueServiceTokenRequest CreateTokenRequest(string clientSecret) => new()
    {
        ClientId = "test-client",
        ClientSecret = clientSecret,
        Audience = XFrameworkServiceNames.Communications
    };

    private static string? ReadCredentialGeneration(string token) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .Single(claim => claim.Type == "client_credential_generation")
            .Value;

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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }
}
