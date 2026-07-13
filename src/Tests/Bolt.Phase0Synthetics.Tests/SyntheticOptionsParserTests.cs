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
        options.CommunicationsToken.ToString().Should().Be("[REDACTED]");
        options.UserToken.ToString().Should().Be("[REDACTED]");
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
        var args = new[] { "--user-token-env", "header.payload.signature" };

        var action = () => CreateParser(environment).Parse(args);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("invalid_token_environment_name");
    }

    [Test]
    public void Parse_DirectTokenCommandLineOption_IsUnknown()
    {
        var environment = CreateValidEnvironment();
        var args = new[] { "--user-token", "header.payload.signature" };

        var action = () => CreateParser(environment).Parse(args);

        action.Should().Throw<SyntheticConfigurationException>()
            .Which.Code.Should().Be("unknown_cli_option");
    }

    [Test]
    public void Parse_AlternativeTokenEnvironmentNames_ReadsIndirectValues()
    {
        var environment = CreateValidEnvironment();
        environment["COMM_TOKEN_CANARY"] = "communications-canary-token";
        environment["USER_TOKEN_CANARY"] = "user-canary-token";
        var args = new[]
        {
            "--communications-token-env", "COMM_TOKEN_CANARY",
            "--user-token-env", "USER_TOKEN_CANARY"
        };

        var options = CreateParser(environment).Parse(args);

        options.CommunicationsToken.Sha256Prefix.Should().NotBe(options.UserToken.Sha256Prefix);
    }

    [Test]
    public void Parse_TokenFilesConfigured_PrefersEachFileAndReadsItOnce()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.CommunicationsTokenFileEnvironmentVariable] = "/run/secrets/communications";
        environment[SyntheticOptionsParser.UserTokenFileEnvironmentVariable] = "/run/secrets/user";
        environment[SyntheticOptionsParser.ExpiryTokenFileEnvironmentVariable] = "/run/secrets/expiry";
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
            "/run/secrets/communications",
            "/run/secrets/user",
            "/run/secrets/expiry");
        reads.Should().OnlyHaveUniqueItems();
        options.UserToken.Sha256Prefix.Should().Be(new SecretToken("file-token-user").Sha256Prefix);
        options.CommunicationsToken.Sha256Prefix.Should().Be(new SecretToken("file-token-communications").Sha256Prefix);
        options.ExpiryToken!.Sha256Prefix.Should().Be(new SecretToken("file-token-expiry").Sha256Prefix);
    }

    [Test]
    public void Parse_ConfiguredTokenFileFails_DoesNotFallBackToEnvironmentToken()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.UserTokenFileEnvironmentVariable] = "/run/secrets/user";
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
        environment[SyntheticOptionsParser.RejectedCommunicationsTokenFileEnvironmentVariable] =
            "/run/secrets/rejected-communications";
        environment[SyntheticOptionsParser.RejectedUserTokenFileEnvironmentVariable] = "/run/secrets/rejected-user";
        var reads = new List<string>();
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            path =>
            {
                reads.Add(path);
                return new SecretToken($"old-token-{Path.GetFileName(path)}");
            });

        var options = parser.Parse([]);

        reads.Should().Equal("/run/secrets/rejected-communications", "/run/secrets/rejected-user");
        options.RejectedCommunicationsToken.Should().NotBeNull();
        options.RejectedUserToken.Should().NotBeNull();
    }

    [Test]
    public void Parse_RejectedGenerationTokenMatchesCurrent_FailsClosed()
    {
        var environment = CreateValidEnvironment();
        environment[SyntheticOptionsParser.RejectedUserTokenFileEnvironmentVariable] = "/run/secrets/rejected-user";
        var parser = new SyntheticOptionsParser(
            name => environment.GetValueOrDefault(name),
            _ => new SecretToken(environment[SyntheticOptionsParser.DefaultUserTokenEnvironmentVariable]));

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
            [SyntheticOptionsParser.DefaultCommunicationsTokenEnvironmentVariable] = "communications-secret-token",
            [SyntheticOptionsParser.DefaultUserTokenEnvironmentVariable] = "user-secret-token"
        };
}
