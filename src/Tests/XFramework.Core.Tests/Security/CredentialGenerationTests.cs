using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;
using XFramework.Integration.Services;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class CredentialGenerationTests
{
    private const string G0Secret = "g0-secret-credential-material-0000000000000000000000000000000000000000";
    private const string G1Secret = "g1-secret-credential-material-1111111111111111111111111111111111111111";
    private static readonly JwtSecurityTokenHandler Handler = new();
    private static readonly string JwtKeyDirectory = Path.Combine(
        Path.GetTempPath(),
        "XFramework.CredentialGenerationTests",
        Guid.NewGuid().ToString("N"));
    private static readonly IReadOnlyDictionary<string, TestJwtKeyPair> JwtKeys =
        new Dictionary<string, TestJwtKeyPair>(StringComparer.Ordinal)
        {
            [G0Secret] = CreateKeyPair("g0"),
            [G1Secret] = CreateKeyPair("g1")
        };

    [OneTimeTearDown]
    public void DeleteJwtKeys()
    {
        if (Directory.Exists(JwtKeyDirectory))
            Directory.Delete(JwtKeyDirectory, recursive: true);
    }

    [Test]
    public async Task JwtValidation_G1PreStagedAsFallback_AcceptsG1ButIssuesWithG0()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g0", G0Secret, "g1", G1Secret, clock.GetUtcNow().AddMinutes(10));
        var service = new JwtService(options, clock);

        var stagedToken = CreateToken("g1", G1Secret, options, clock.GetUtcNow());
        var (stagedPrincipal, _) = await service.DecodeJwtToken(stagedToken);
        var issued = await service.GenerateToken("user", Guid.NewGuid(), []);
        var issuedToken = Handler.ReadJwtToken(issued.AccessToken);

        stagedPrincipal.FindFirst(JwtCredentialSet.GenerationClaim)?.Value.Should().Be("g1");
        issuedToken.Header.Kid.Should().Be("g0");
        issuedToken.Payload.NotBefore.Should().Be(clock.GetUtcNow().ToUnixTimeSeconds());
        issuedToken.Claims.Single(claim => claim.Type == JwtCredentialSet.GenerationClaim).Value.Should().Be("g0");
    }

    [Test]
    public async Task JwtValidation_G1CurrentWithG0Fallback_AcceptsG0ButIssuesWithG1()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(10));
        var service = new JwtService(options, clock);

        var oldToken = CreateToken("g0", G0Secret, options, clock.GetUtcNow());
        var (oldPrincipal, _) = await service.DecodeJwtToken(oldToken);
        var issued = await service.GenerateToken("user", Guid.NewGuid(), []);
        var issuedToken = Handler.ReadJwtToken(issued.AccessToken);

        oldPrincipal.FindFirst(JwtCredentialSet.GenerationClaim)?.Value.Should().Be("g0");
        issuedToken.Header.Kid.Should().Be("g1");
        issuedToken.Claims.Single(claim => claim.Type == JwtCredentialSet.GenerationClaim).Value.Should().Be("g1");
    }

    [Test]
    public async Task JwtIssuance_ProvidedFallbackGenerationClaim_IsReplacedWithCurrentGeneration()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(10));
        var service = new JwtService(options, clock);

        var issued = await service.GenerateToken(
        [
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim(JwtCredentialSet.GenerationClaim, "g0")
        ]);
        var token = Handler.ReadJwtToken(issued.AccessToken);

        token.Header.Kid.Should().Be("g1");
        token.Claims.Where(claim => claim.Type == JwtCredentialSet.GenerationClaim)
            .Select(claim => claim.Value)
            .Should().Equal("g1");
    }

    [Test]
    public async Task JwtValidation_FallbackDeadlinePasses_RetiresFallbackAndKeepsCurrent()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var service = new JwtService(options, clock);
        var oldToken = CreateToken("g0", G0Secret, options, clock.GetUtcNow());
        var currentToken = CreateToken("g1", G1Secret, options, clock.GetUtcNow());

        await service.DecodeJwtToken(oldToken);
        clock.Advance(TimeSpan.FromMinutes(6));

        var decodeOld = async () => await service.DecodeJwtToken(oldToken);
        await decodeOld.Should().ThrowAsync<SecurityTokenException>();
        await service.DecodeJwtToken(currentToken);
    }

    [Test]
    public async Task JwtValidation_AtFallbackDeadline_RejectsFallback()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var deadline = clock.GetUtcNow().AddMinutes(5);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, deadline);
        var service = new JwtService(options, clock);
        var oldToken = CreateToken("g0", G0Secret, options, clock.GetUtcNow());
        clock.Advance(TimeSpan.FromMinutes(5));

        var decode = async () => await service.DecodeJwtToken(oldToken);

        await decode.Should().ThrowAsync<SecurityTokenException>();
    }

    [Test]
    public async Task JwtValidation_GenerationClaimDoesNotMatchSigningKey_RejectsToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var token = CreateToken("g0", G1Secret, options, clock.GetUtcNow(), signingKeyId: "g1");
        var service = new JwtService(options, clock);

        var decode = async () => await service.DecodeJwtToken(token);

        await decode.Should().ThrowAsync<SecurityTokenException>();
    }

    [Test]
    public async Task JwtValidation_MissingGenerationClaim_RejectsToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var key = CreatePrivateSecurityKey(G1Secret, "g1");
        var token = new JwtSecurityToken(
            options.ValidIssuer,
            options.ValidAudience,
            expires: clock.GetUtcNow().UtcDateTime.AddMinutes(20),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha512));
        var service = new JwtService(options, clock);

        var decode = async () => await service.DecodeJwtToken(Handler.WriteToken(token));

        await decode.Should().ThrowAsync<SecurityTokenException>();
    }

    [Test]
    public async Task JwtValidation_TokenWithoutExpiration_RejectsToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var token = CreateTokenWithoutExpiration("g1", G1Secret, options);
        var service = new JwtService(options, clock);

        var decode = async () => await service.DecodeJwtToken(token);

        await decode.Should().ThrowAsync<SecurityTokenNoExpirationException>();
    }

    [Test]
    public async Task JwtRefreshValidation_TokenWithoutExpiration_RejectsToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var token = CreateTokenWithoutExpiration("g1", G1Secret, options);
        var service = new JwtService(options, clock);

        var decode = async () => await service.DecodeExpiredToken(token);

        await decode.Should().ThrowAsync<SecurityTokenException>();
    }

    [Test]
    public async Task JwtRefreshValidation_ExpiredTokenWithExpiration_AcceptsToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var options = CreateJwtOptions("g1", G1Secret, "g0", G0Secret, clock.GetUtcNow().AddMinutes(5));
        var token = CreateToken(
            "g1",
            G1Secret,
            options,
            clock.GetUtcNow().AddMinutes(-30));
        var service = new JwtService(options, clock);

        var (principal, _) = await service.DecodeExpiredToken(token);

        principal.FindFirst(JwtCredentialSet.GenerationClaim)?.Value.Should().Be("g1");
    }

    [Test]
    public void CredentialValidation_InvalidRotationConfiguration_Fails()
    {
        var now = DateTimeOffset.UtcNow;

        AssertInvalid(new("", G0Secret), null, now);
        AssertInvalid(new("g0", "weak"), null, now);
        AssertInvalid(new("g0", G0Secret), new("", G1Secret, now.AddMinutes(5)), now);
        AssertInvalid(new("g0", G0Secret), new("g0", G1Secret, now.AddMinutes(5)), now);
        AssertInvalid(new("g0", G0Secret), new("g1", G0Secret, now.AddMinutes(5)), now);
        AssertInvalid(new("g0", G0Secret), new("g1", G1Secret), now);
        AssertInvalid(new("g0", G0Secret), new("g1", G1Secret, now), now);
        AssertInvalid(
            new("g0", G0Secret),
            new("g1", G1Secret, new DateTimeOffset(now.AddMinutes(5).DateTime, TimeSpan.FromHours(8))),
            now);
    }

    [Test]
    public void CredentialValidation_FallbackLifetime_IsBoundedToEightHours()
    {
        var now = DateTimeOffset.UtcNow;
        var validateAtBoundary = () => CredentialGenerationValidator.Validate(
            "Credentials",
            new("g0", G0Secret),
            new("g1", G1Secret, now.Add(CredentialGenerationValidator.MaximumValidationFallbackLifetime)),
            now);
        var validatePastBoundary = () => CredentialGenerationValidator.Validate(
            "Credentials",
            new("g0", G0Secret),
            new(
                "g1",
                G1Secret,
                now.Add(CredentialGenerationValidator.MaximumValidationFallbackLifetime).AddTicks(1)),
            now);

        validateAtBoundary.Should().NotThrow();
        validatePastBoundary.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void OptionsBinding_AllEmptyFallbackObjects_AreTreatedAsNotConfigured()
    {
        var now = DateTimeOffset.UtcNow;
        var jwtOptions = CreateJwtOptions("g0", G0Secret, "", "", now.AddMinutes(5));
        jwtOptions.ValidationFallback!.ValidUntilUtc = null;
        var serviceIdentityOptions = new ServiceIdentityOptions
        {
            GenerationId = "g0",
            ClientSecret = G0Secret,
            ValidationFallback = new ServiceIdentityValidationFallbackOptions()
        };

        var validateJwt = () => JwtCredentialSet.Validate(jwtOptions, now);
        var validateServiceIdentity = () => serviceIdentityOptions.ValidateClientCredential(now);

        validateJwt.Should().NotThrow();
        JwtCredentialSet.ResolveValidationKeys(jwtOptions, keyId: null, now).Should().ContainSingle();
        jwtOptions.ValidationGenerationIds.Should().Equal("g0");
        validateServiceIdentity.Should().NotThrow();
        serviceIdentityOptions.ValidationGenerationIds.Should().Equal("g0");
    }

    [Test]
    public void OptionsBinding_PartialFallbackObjects_AreRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var jwtOptions = CreateJwtOptions("g0", G0Secret, "g1", "", now.AddMinutes(5));
        var serviceIdentityOptions = new ServiceIdentityOptions
        {
            GenerationId = "g0",
            ClientSecret = G0Secret,
            ValidationFallback = new ServiceIdentityValidationFallbackOptions
            {
                ClientSecret = G1Secret
            }
        };

        var validateJwt = () => JwtCredentialSet.Validate(jwtOptions, now);
        var validateServiceIdentity = () => serviceIdentityOptions.ValidateClientCredential(now);

        validateJwt.Should().Throw<InvalidOperationException>();
        validateServiceIdentity.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ServiceIdentityOptions_MissingOrPartialCurrentCredential_IsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var options = new[]
        {
            new ServiceIdentityOptions(),
            new ServiceIdentityOptions { GenerationId = "g0" },
            new ServiceIdentityOptions { ClientSecret = G0Secret }
        };

        foreach (var candidate in options)
        {
            var validate = () => candidate.ValidateClientCredential(now);
            validate.Should().Throw<InvalidOperationException>();
        }
    }

    [Test]
    public void FixedTimeSecretComparison_HandlesEqualAndDifferentLengthInputsWithoutDisclosingValues()
    {
        CredentialGenerationValidator.FixedTimeEquals(G0Secret, G0Secret).Should().BeTrue();
        CredentialGenerationValidator.FixedTimeEquals(G0Secret, G1Secret).Should().BeFalse();
        CredentialGenerationValidator.FixedTimeEquals(G0Secret, G0Secret[..^1]).Should().BeFalse();
        CredentialGenerationValidator.FixedTimeEquals(G0Secret, $"{G0Secret}x").Should().BeFalse();
        CredentialGenerationValidator.FixedTimeEquals(G0Secret, null).Should().BeFalse();

        var validate = () => CredentialGenerationValidator.Validate(
            "ServiceIdentity",
            new CredentialGenerationDescriptor("g0", G0Secret),
            new CredentialGenerationDescriptor("g1", G0Secret, DateTimeOffset.UtcNow.AddMinutes(5)),
            DateTimeOffset.UtcNow);

        validate.Should().Throw<InvalidOperationException>()
            .Where(exception => !exception.Message.Contains(G0Secret, StringComparison.Ordinal));
    }

    [Test]
    public async Task CredentialGenerationHealthCheck_ReportsOnlySafeConvergenceMetadata()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var validUntil = clock.GetUtcNow().AddMinutes(10);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtOptions:GenerationId"] = "jwt-g1",
                ["JwtOptions:SigningPublicKeyPath"] = JwtKeys[G1Secret].PublicKeyPath,
                ["JwtOptions:ValidationFallback:GenerationId"] = "jwt-g0",
                ["JwtOptions:ValidationFallback:SigningPublicKeyPath"] = JwtKeys[G0Secret].PublicKeyPath,
                ["JwtOptions:ValidationFallback:ValidUntilUtc"] = validUntil.ToString("O"),
                ["ServiceIdentity:GenerationId"] = "client-g1",
                ["ServiceIdentity:ClientSecret"] = G1Secret,
                ["ServiceIdentity:ValidationFallback:GenerationId"] = "client-g0",
                ["ServiceIdentity:ValidationFallback:ClientSecret"] = G0Secret,
                ["ServiceIdentity:ValidationFallback:ValidUntilUtc"] = validUntil.ToString("O"),
                ["ServiceIdentity:Clients:0:ClientId"] = "health-client",
                ["ServiceIdentity:Clients:0:GenerationId"] = "health-g1",
                ["ServiceIdentity:Clients:0:ClientSecret"] = G1Secret,
                ["ServiceIdentity:Clients:0:ValidationFallback:GenerationId"] = "health-g0",
                ["ServiceIdentity:Clients:0:ValidationFallback:ClientSecret"] = G0Secret,
                ["ServiceIdentity:Clients:0:ValidationFallback:ValidUntilUtc"] = validUntil.ToString("O")
            })
            .Build();
        var serviceOptions = new ServiceIdentityOptions();
        configuration.GetSection(ServiceIdentityOptions.SectionName).Bind(serviceOptions);
        var services = new ServiceCollection()
            .AddSingleton<IOptions<ServiceIdentityOptions>>(Options.Create(serviceOptions))
            .BuildServiceProvider();
        var healthCheck = new CredentialGenerationHealthCheck(configuration, services, clock);

        var result = await healthCheck.CheckHealthAsync(new());
        var json = JsonSerializer.Serialize(result.Data);

        json.Should().Contain("jwt-g1").And.Contain("jwt-g0");
        json.Should().Contain("client-g1").And.Contain("client-g0");
        json.Should().Contain("health-g1").And.Contain("health-g0");
        json.Should().Contain("validationFallbackValidUntilUtc");
        json.Should().NotContain(G0Secret).And.NotContain(G1Secret);
    }

    [Test]
    public void CredentialGenerationHealthCheck_Registration_IsIdempotentAndReadinessTagged()
    {
        var services = new ServiceCollection();
        services.AddCredentialGenerationHealthCheck();
        services.AddCredentialGenerationHealthCheck();
        using var provider = services.BuildServiceProvider();

        var registrations = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations
            .Where(registration => registration.Name == "credential-generations")
            .ToList();

        registrations.Should().ContainSingle();
        registrations[0].Tags.Should().Contain("ready");
    }

    [Test]
    public void JwtCredentialSet_RejectsRsaKeysBelow2048Bits()
    {
        var directory = Path.Combine(JwtKeyDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var privatePath = Path.Combine(directory, "private.pem");
        var publicPath = Path.Combine(directory, "public.pem");
        using (var rsa = RSA.Create(1024))
        {
            File.WriteAllText(privatePath, rsa.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem());
        }

        var options = new JwtOptions
        {
            GenerationId = "weak-key-test",
            SigningPrivateKeyPath = privatePath,
            SigningPublicKeyPath = publicPath,
            ValidIssuer = "credential-tests",
            ValidAudience = "credential-tests"
        };

        var validate = () => JwtCredentialSet.Validate(options, DateTimeOffset.UtcNow);

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 2048 bits*");
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    public void JwtCredentialSet_MissingSigningKeysOutsideDevelopmentOrTest_FailsClosed(
        string environmentName)
    {
        var directory = Path.Combine(JwtKeyDirectory, Guid.NewGuid().ToString("N"));
        var options = MissingJwtKeyOptions(directory);

        var validate = () => JwtCredentialSet.Validate(
            options,
            DateTimeOffset.UtcNow,
            environmentName);

        validate.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be provisioned outside Development or Test environments*");
        File.Exists(options.SigningPrivateKeyPath).Should().BeFalse();
        File.Exists(options.SigningPublicKeyPath).Should().BeFalse();
    }

    [TestCase("Development")]
    [TestCase("Test")]
    public void JwtCredentialSet_MissingSigningKeysInAllowedEnvironment_CreatesSecureKeyPair(
        string environmentName)
    {
        var directory = Path.Combine(JwtKeyDirectory, Guid.NewGuid().ToString("N"));
        var options = MissingJwtKeyOptions(directory);

        JwtCredentialSet.Validate(options, DateTimeOffset.UtcNow, environmentName);

        File.Exists(options.SigningPrivateKeyPath).Should().BeTrue();
        File.Exists(options.SigningPublicKeyPath).Should().BeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(options.SigningPrivateKeyPath!).Should().Be(
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static JwtOptions MissingJwtKeyOptions(string directory) => new()
    {
        GenerationId = $"generated-{Guid.NewGuid():N}",
        SigningPrivateKeyPath = Path.Combine(directory, "private.pem"),
        SigningPublicKeyPath = Path.Combine(directory, "public.pem"),
        ValidIssuer = "credential-tests",
        ValidAudience = "credential-tests"
    };

    private static void AssertInvalid(
        CredentialGenerationDescriptor current,
        CredentialGenerationDescriptor? fallback,
        DateTimeOffset now)
    {
        var validate = () => CredentialGenerationValidator.Validate("Credentials", current, fallback, now);
        validate.Should().Throw<InvalidOperationException>();
    }

    private static JwtOptions CreateJwtOptions(
        string currentId,
        string currentSecret,
        string fallbackId,
        string fallbackSecret,
        DateTimeOffset validUntilUtc) => new()
    {
        GenerationId = currentId,
        SigningPrivateKeyPath = JwtKeys[currentSecret].PrivateKeyPath,
        SigningPublicKeyPath = JwtKeys[currentSecret].PublicKeyPath,
        ValidationFallback = new JwtValidationFallbackOptions
        {
            GenerationId = fallbackId,
            SigningPublicKeyPath = string.IsNullOrWhiteSpace(fallbackSecret)
                ? string.Empty
                : JwtKeys[fallbackSecret].PublicKeyPath,
            ValidUntilUtc = validUntilUtc
        },
        ValidIssuer = "credential-tests",
        ValidAudience = "credential-tests",
        AccessTokenLifespan = "00:30:00",
        RefreshTokenLifespan = "01:00:00"
    };

    private static string CreateToken(
        string generationId,
        string secret,
        JwtOptions options,
        DateTimeOffset now,
        string? signingKeyId = null)
    {
        var key = CreatePrivateSecurityKey(secret, signingKeyId ?? generationId);
        var token = new JwtSecurityToken(
            options.ValidIssuer,
            options.ValidAudience,
            [new Claim(JwtCredentialSet.GenerationClaim, generationId)],
            expires: now.UtcDateTime.AddMinutes(20),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha512));
        return Handler.WriteToken(token);
    }

    private static string CreateTokenWithoutExpiration(
        string generationId,
        string secret,
        JwtOptions options)
    {
        var key = CreatePrivateSecurityKey(secret, generationId);
        var token = new JwtSecurityToken(
            options.ValidIssuer,
            options.ValidAudience,
            [new Claim(JwtCredentialSet.GenerationClaim, generationId)],
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha512));
        return Handler.WriteToken(token);
    }

    private static TestJwtKeyPair CreateKeyPair(string generationId)
    {
        Directory.CreateDirectory(JwtKeyDirectory);
        var privateKeyPath = Path.Combine(JwtKeyDirectory, $"{generationId}-private.pem");
        var publicKeyPath = Path.Combine(JwtKeyDirectory, $"{generationId}-public.pem");
        using var rsa = RSA.Create(2048);
        File.WriteAllText(privateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        return new TestJwtKeyPair(privateKeyPath, publicKeyPath);
    }

    private static RsaSecurityKey CreatePrivateSecurityKey(string keyName, string keyId)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(JwtKeys[keyName].PrivateKeyPath));
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }

    private sealed record TestJwtKeyPair(string PrivateKeyPath, string PublicKeyPath);
}
