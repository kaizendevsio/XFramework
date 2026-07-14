using FluentAssertions;
using XFramework.Bolt.Phase0Synthetics;

namespace Bolt.Phase0Synthetics.Tests;

public sealed class SyntheticOptionsParserTests
{
    private static readonly Guid TenantId = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid CredentialId = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Test]
    public void Parse_ValidEnvironmentInputs_ReturnsValidatedOptions()
    {
        var environment = CreateValidEnvironment();

        var options = CreateParser(environment).Parse([]);

        options.Target.Should().Be(new Uri("wss://bolt.example.test/bolt/ws"));
        options.TenantId.Should().Be(TenantId);
        options.CredentialId.Should().Be(CredentialId);
        options.DeviceId.Should().Be("phase0_device");
        options.CommunicationsTransportToken.ToString().Should().Be("[REDACTED]");
        options.PortalTransportToken.ToString().Should().Be("[REDACTED]");
        options.UserActorToken.ToString().Should().Be("[REDACTED]");
    }

    [TestCase("ws://bolt.example.test/bolt/ws")]
    [TestCase("https://bolt.example.test/bolt/ws")]
    [TestCase("wss://bolt.example.test/bolt/ws?access_token=secret")]
    [TestCase("wss://user:secret@bolt.example.test/bolt/ws")]
    public void Parse_InsecureOrSecretBearingTarget_FailsClosed(string target)
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.TargetEnvironmentVariable] = target;

        var action = () => CreateParser(environment).Parse([]);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_wss_target");
    }

    [Test]
    public void Parse_MissingRequiredInput_FailsClosed()
    {
        var environment = CreateValidEnvironment();
        environment.Remove(SyntheticOptionsParser.CredentialEnvironmentVariable);

        var action = () => CreateParser(environment).Parse([]);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("missing_credential_id");
    }

    [Test]
    public void Parse_TokenValuePassedAsEnvironmentName_RejectsTokenLikeValue()
    {
        var environment = CreateValidEnvironment();
        var args = new[] { "--user-actor-token-env", "header.payload.signature" };

        var action = () => CreateParser(environment).Parse(args);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_token_environment_name");
    }

    [Test]
    public void Parse_DirectTokenCommandLineOption_IsUnknown()
    {
        var environment = CreateValidEnvironment();
        var args = new[] { "--user-actor-token", "header.payload.signature" };

        var action = () => CreateParser(environment).Parse(args);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("unknown_cli_option");
    }

    [Test]
    public void Parse_AlternativeTokenEnvironmentNames_ReadsIndirectValues()
    {
        var environment = CreateValidEnvironment();
        environment["COMM_TRANSPORT_CANARY"] = "communications-transport-canary-token";
        environment["PORTAL_TRANSPORT_CANARY"] = "portal-transport-canary-token";
        environment["USER_ACTOR_CANARY"] = "user-actor-canary-token";
        var args = new[]
        {
            "--communications-transport-token-env", "COMM_TRANSPORT_CANARY",
            "--portal-transport-token-env", "PORTAL_TRANSPORT_CANARY",
            "--user-actor-token-env", "USER_ACTOR_CANARY"
        };

        var options = CreateParser(environment).Parse(args);

        options.CommunicationsTransportToken.Sha256Prefix.Should().NotBe(options.PortalTransportToken.Sha256Prefix);
        options.PortalTransportToken.Sha256Prefix.Should().NotBe(options.UserActorToken.Sha256Prefix);
    }

    [Test]
    public void Parse_TokenFilesConfigured_PrefersEachFileAndReadsItOnce()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.CommunicationsTransportTokenFileEnvironmentVariable] =
            "/run/secrets/communications-transport";
        environment[SyntheticOptionsParser.PortalTransportTokenFileEnvironmentVariable] =
            "/run/secrets/portal-transport";
        environment[SyntheticOptionsParser.UserActorTokenFileEnvironmentVariable] = "/run/secrets/user-actor";
        environment[SyntheticOptionsParser.ExpiryTransportTokenFileEnvironmentVariable] =
            "/run/secrets/expiry-transport";
        var reads = new List<string>();
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            path =>
            {
                reads.Add(path);
                return new SecretToken($"file-token-{Path.GetFileName(path)}");
            });

        var options = parser.Parse([]);

        reads.Should().Equal(
            "/run/secrets/communications-transport",
            "/run/secrets/portal-transport",
            "/run/secrets/user-actor",
            "/run/secrets/expiry-transport");
        reads.Should().OnlyHaveUniqueItems();
        options.UserActorToken.Sha256Prefix.Should().Be(new SecretToken("file-token-user-actor").Sha256Prefix);
        options.CommunicationsTransportToken.Sha256Prefix.Should()
            .Be(new SecretToken("file-token-communications-transport").Sha256Prefix);
        options.PortalTransportToken.Sha256Prefix.Should()
            .Be(new SecretToken("file-token-portal-transport").Sha256Prefix);
        options.ExpiryTransportToken!.Sha256Prefix.Should()
            .Be(new SecretToken("file-token-expiry-transport").Sha256Prefix);
    }

    [Test]
    public void Parse_ConfiguredTokenFileFails_DoesNotFallBackToEnvironmentToken()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.UserActorTokenFileEnvironmentVariable] = "/run/secrets/user-actor";
        var reads = 0;
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            _ =>
            {
                reads++;
                throw new SyntheticConfigurationException("invalid_token_file_permissions");
            });

        var action = () => parser.Parse([]);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_token_file_permissions");
        reads.Should().Be(1);
    }

    [Test]
    public void Parse_RejectedGenerationTokens_ReadsOnlyFromFiles()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.RejectedCommunicationsTransportTokenFileEnvironmentVariable] =
            "/run/secrets/rejected-communications-transport";
        environment[SyntheticOptionsParser.RejectedPortalTransportTokenFileEnvironmentVariable] =
            "/run/secrets/rejected-portal-transport";
        var reads = new List<string>();
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            path =>
            {
                reads.Add(path);
                return new SecretToken($"old-token-{Path.GetFileName(path)}");
            });

        var options = parser.Parse([]);

        reads.Should().Equal(
            "/run/secrets/rejected-communications-transport",
            "/run/secrets/rejected-portal-transport");
        options.RejectedCommunicationsTransportToken.Should().NotBeNull();
        options.RejectedPortalTransportToken.Should().NotBeNull();
    }

    [Test]
    public void Parse_RejectedGenerationTokenMatchesCurrent_FailsClosed()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.RejectedPortalTransportTokenFileEnvironmentVariable] =
            "/run/secrets/rejected-portal-transport";
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            _ => new SecretToken(environment[SyntheticOptionsParser.DefaultPortalTransportTokenEnvironmentVariable]));

        var action = () => parser.Parse([]);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("old_generation_token_matches_current");
    }

    private static SyntheticOptionsParser CreateParser(IReadOnlyDictionary<string, string> environment) =>
        new(name => environment.GetValueOrDefault(name));

    private static Dictionary<string, string> CreateValidEnvironment() =>
        new(StringComparer.Ordinal)
        {
            [SyntheticOptionsParser.TargetEnvironmentVariable] = "wss://bolt.example.test/bolt/ws",
            [SyntheticOptionsParser.TenantEnvironmentVariable] = TenantId.ToString("D"),
            [SyntheticOptionsParser.CredentialEnvironmentVariable] = CredentialId.ToString("D"),
            [SyntheticOptionsParser.DeviceEnvironmentVariable] = "phase0_device",
            [SyntheticOptionsParser.DefaultCommunicationsTransportTokenEnvironmentVariable] =
                "communications-transport-secret-token",
            [SyntheticOptionsParser.DefaultPortalTransportTokenEnvironmentVariable] =
                "portal-transport-secret-token",
            [SyntheticOptionsParser.DefaultUserActorTokenEnvironmentVariable] = "user-actor-secret-token"
        };
}
