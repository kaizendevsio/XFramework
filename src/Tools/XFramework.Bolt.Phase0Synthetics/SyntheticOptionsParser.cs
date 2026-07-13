using System.Globalization;

namespace XFramework.Bolt.Phase0Synthetics;

public sealed class SyntheticOptionsParser(
    Func<string, string?> readEnvironment,
    Func<string, SecretToken>? readTokenFile = null)
{
    public const string TargetEnvironmentVariable = "BOLT_SYNTHETIC_TARGET";
    public const string TenantEnvironmentVariable = "BOLT_SYNTHETIC_TENANT_ID";
    public const string CredentialEnvironmentVariable = "BOLT_SYNTHETIC_CREDENTIAL_ID";
    public const string DeviceEnvironmentVariable = "BOLT_SYNTHETIC_DEVICE_ID";
    public const string CommunicationsTokenEnvironmentNameVariable = "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_ENV";
    public const string UserTokenEnvironmentNameVariable = "BOLT_SYNTHETIC_USER_TOKEN_ENV";
    public const string ExpiryTokenEnvironmentNameVariable = "BOLT_SYNTHETIC_EXPIRY_TOKEN_ENV";
    public const string DefaultCommunicationsTokenEnvironmentVariable = "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN";
    public const string DefaultUserTokenEnvironmentVariable = "BOLT_SYNTHETIC_USER_TOKEN";
    public const string DefaultExpiryTokenEnvironmentVariable = "BOLT_SYNTHETIC_EXPIRY_TOKEN";
    public const string CommunicationsTokenFileEnvironmentVariable = "BOLT_SYNTHETIC_COMMUNICATIONS_TOKEN_FILE";
    public const string UserTokenFileEnvironmentVariable = "BOLT_SYNTHETIC_USER_TOKEN_FILE";
    public const string ExpiryTokenFileEnvironmentVariable = "BOLT_SYNTHETIC_EXPIRY_TOKEN_FILE";
    public const string RejectedCommunicationsTokenFileEnvironmentVariable =
        "BOLT_SYNTHETIC_REJECTED_COMMUNICATIONS_TOKEN_FILE";
    public const string RejectedUserTokenFileEnvironmentVariable = "BOLT_SYNTHETIC_REJECTED_USER_TOKEN_FILE";

    private readonly Func<string, SecretToken> _readTokenFile =
        readTokenFile ?? SecretTokenFileReader.Read;

    private static readonly HashSet<string> AllowedOptions =
    [
        "--target",
        "--tenant-id",
        "--credential-id",
        "--device-id",
        "--communications-token-env",
        "--user-token-env",
        "--operation-timeout-seconds",
        "--expiry-token-env",
        "--expiry-grace-seconds",
        "--expiry-max-wait-seconds"
    ];

    public static SyntheticOptionsParser CreateDefault() =>
        new(Environment.GetEnvironmentVariable);

    public SyntheticOptions Parse(IReadOnlyList<string> args)
    {
        var cli = ParseCommandLine(args);
        var targetValue = RequiredValue(cli, "--target", TargetEnvironmentVariable, "missing_target");
        if (!Uri.TryCreate(targetValue, UriKind.Absolute, out var target))
            throw new SyntheticConfigurationException("invalid_wss_target");

        var tenantId = ParseGuid(
            RequiredValue(cli, "--tenant-id", TenantEnvironmentVariable, "missing_tenant_id"),
            "invalid_tenant_id");
        var credentialId = ParseGuid(
            RequiredValue(cli, "--credential-id", CredentialEnvironmentVariable, "missing_credential_id"),
            "invalid_credential_id");
        var deviceId = RequiredValue(cli, "--device-id", DeviceEnvironmentVariable, "missing_device_id");

        var communicationsToken = ReadToken(
            CommunicationsTokenFileEnvironmentVariable,
            TokenEnvironmentName(cli, "--communications-token-env", CommunicationsTokenEnvironmentNameVariable,
                DefaultCommunicationsTokenEnvironmentVariable),
            "missing_communications_token");
        var userToken = ReadToken(
            UserTokenFileEnvironmentVariable,
            TokenEnvironmentName(cli, "--user-token-env", UserTokenEnvironmentNameVariable,
                DefaultUserTokenEnvironmentVariable),
            "missing_user_token");

        var operationTimeout = TimeSpan.FromSeconds(ParseInteger(
            OptionalValue(cli, "--operation-timeout-seconds", "BOLT_SYNTHETIC_OPERATION_TIMEOUT_SECONDS") ?? "30",
            "invalid_operation_timeout"));
        var expiryGrace = TimeSpan.FromSeconds(ParseInteger(
            OptionalValue(cli, "--expiry-grace-seconds", "BOLT_SYNTHETIC_EXPIRY_GRACE_SECONDS") ?? "10",
            "invalid_expiry_grace"));
        var expiryMaxWait = TimeSpan.FromSeconds(ParseInteger(
            OptionalValue(cli, "--expiry-max-wait-seconds", "BOLT_SYNTHETIC_EXPIRY_MAX_WAIT_SECONDS") ?? "180",
            "invalid_expiry_max_wait"));

        var expiryToken = ReadOptionalExpiryToken(cli);
        var rejectedCommunicationsToken = ReadOptionalFileToken(
            RejectedCommunicationsTokenFileEnvironmentVariable);
        var rejectedUserToken = ReadOptionalFileToken(RejectedUserTokenFileEnvironmentVariable);
        var options = new SyntheticOptions(
            target,
            tenantId,
            credentialId,
            deviceId,
            communicationsToken,
            userToken,
            operationTimeout,
            expiryToken,
            expiryGrace,
            expiryMaxWait,
            rejectedCommunicationsToken,
            rejectedUserToken);
        SyntheticOptionsValidator.Validate(options);
        return options;
    }

    private SecretToken? ReadOptionalFileToken(string environmentVariable)
    {
        var tokenFile = readEnvironment(environmentVariable)?.Trim();
        return string.IsNullOrWhiteSpace(tokenFile) ? null : _readTokenFile(tokenFile);
    }

    private SecretToken? ReadOptionalExpiryToken(IReadOnlyDictionary<string, string> cli)
    {
        var tokenFile = readEnvironment(ExpiryTokenFileEnvironmentVariable)?.Trim();
        if (!string.IsNullOrWhiteSpace(tokenFile))
            return _readTokenFile(tokenFile);

        var configuredName = OptionalValue(cli, "--expiry-token-env", ExpiryTokenEnvironmentNameVariable);
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            var defaultValue = readEnvironment(DefaultExpiryTokenEnvironmentVariable);
            return string.IsNullOrWhiteSpace(defaultValue) ? null : new SecretToken(defaultValue);
        }

        return ReadEnvironmentToken(configuredName, "missing_expiry_token");
    }

    private SecretToken ReadToken(
        string tokenFileEnvironmentVariable,
        string environmentVariableName,
        string missingCode)
    {
        var tokenFile = readEnvironment(tokenFileEnvironmentVariable)?.Trim();
        return !string.IsNullOrWhiteSpace(tokenFile)
            ? _readTokenFile(tokenFile)
            : ReadEnvironmentToken(environmentVariableName, missingCode);
    }

    private SecretToken ReadEnvironmentToken(string environmentVariableName, string missingCode)
    {
        ValidateEnvironmentVariableName(environmentVariableName);
        var value = readEnvironment(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
            throw new SyntheticConfigurationException(missingCode);

        return new SecretToken(value);
    }

    private string TokenEnvironmentName(
        IReadOnlyDictionary<string, string> cli,
        string optionName,
        string configurationEnvironmentVariable,
        string defaultName) =>
        OptionalValue(cli, optionName, configurationEnvironmentVariable) ?? defaultName;

    private string RequiredValue(
        IReadOnlyDictionary<string, string> cli,
        string optionName,
        string environmentVariable,
        string missingCode)
    {
        var value = OptionalValue(cli, optionName, environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            throw new SyntheticConfigurationException(missingCode);

        return value;
    }

    private string? OptionalValue(
        IReadOnlyDictionary<string, string> cli,
        string optionName,
        string environmentVariable)
    {
        if (cli.TryGetValue(optionName, out var commandLineValue))
            return commandLineValue.Trim();

        return readEnvironment(environmentVariable)?.Trim();
    }

    private static Dictionary<string, string> ParseCommandLine(IReadOnlyList<string> args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            var separatorIndex = argument.IndexOf('=');
            string optionName;
            string value;
            if (separatorIndex > 0)
            {
                optionName = argument[..separatorIndex];
                value = argument[(separatorIndex + 1)..];
            }
            else
            {
                optionName = argument;
                if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new SyntheticConfigurationException("missing_cli_option_value");

                value = args[++index];
            }

            if (!AllowedOptions.Contains(optionName))
                throw new SyntheticConfigurationException("unknown_cli_option");

            if (!result.TryAdd(optionName, value))
                throw new SyntheticConfigurationException("duplicate_cli_option");
        }

        return result;
    }

    private static Guid ParseGuid(string value, string code) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new SyntheticConfigurationException(code);

    private static int ParseInteger(string value, string code) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new SyntheticConfigurationException(code);

    private static void ValidateEnvironmentVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(char.IsAsciiLetter(value[0]) || value[0] == '_') ||
            value.Any(static ch => !(char.IsAsciiLetterOrDigit(ch) || ch == '_')))
        {
            throw new SyntheticConfigurationException("invalid_token_environment_name");
        }
    }
}
