using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Services;

public sealed class ServiceIdentityConfiguration
{
    public const int DefaultBoltTransportTokenLifetimeSeconds = 120;
    public const int MinimumBoltTransportTokenLifetimeSeconds = 60;
    public const int MaximumBoltTransportTokenLifetimeSeconds = 180;

    private readonly IReadOnlyDictionary<string, ServiceClientDefinition> _clients;

    private ServiceIdentityConfiguration(
        string issuer,
        int tokenLifetimeMinutes,
        bool allowInsecureHttp,
        Uri? boltTransportDiscoveryAuthority,
        bool boltTransportTokenIssuerEnabled,
        int boltTransportTokenLifetimeSeconds,
        string? boltTransportSigningKeyPath,
        IReadOnlyDictionary<string, ServiceClientDefinition> clients)
    {
        Issuer = issuer;
        TokenLifetimeMinutes = tokenLifetimeMinutes;
        AllowInsecureHttp = allowInsecureHttp;
        BoltTransportDiscoveryAuthority = boltTransportDiscoveryAuthority;
        BoltTransportTokenIssuerEnabled = boltTransportTokenIssuerEnabled;
        BoltTransportTokenLifetimeSeconds = boltTransportTokenLifetimeSeconds;
        BoltTransportSigningKeyPath = boltTransportSigningKeyPath;
        _clients = clients;
        ValidationGenerationIdsByClient = clients.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ValidationGenerationIds,
            StringComparer.Ordinal);
    }

    public string Issuer { get; }
    public int TokenLifetimeMinutes { get; }
    public bool AllowInsecureHttp { get; }
    public Uri? BoltTransportDiscoveryAuthority { get; }
    public bool BoltTransportTokenIssuerEnabled { get; }
    public int BoltTransportTokenLifetimeSeconds { get; }
    public string? BoltTransportSigningKeyPath { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationGenerationIdsByClient { get; }

    internal ServiceClientDefinition? FindClient(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) && _clients.TryGetValue(clientId, out var client)
            ? client
            : null;

    public static ServiceIdentityConfiguration FromConfiguration(
        IConfiguration configuration,
        DateTimeOffset nowUtc,
        string environmentName = "Production")
    {
        var clients = new Dictionary<string, ServiceClientDefinition>(StringComparer.Ordinal);
        foreach (var section in configuration.GetSection("ServiceIdentity:Clients").GetChildren())
        {
            var client = ServiceClientDefinition.FromConfiguration(section, nowUtc);
            if (!clients.TryAdd(client.ClientId, client))
            {
                throw new InvalidOperationException(
                    $"ServiceIdentity:Clients contains duplicate ClientId '{client.ClientId}'.");
            }
        }

        if (clients.Count == 0)
            throw new InvalidOperationException("ServiceIdentity:Clients must contain at least one service client.");

        var issuer = configuration["ServiceIdentity:Issuer"]?.Trim();
        if (string.IsNullOrWhiteSpace(issuer))
            issuer = XFrameworkServiceNames.IdentityServer;

        var allowInsecureHttp = configuration.GetValue<bool>("ServiceIdentity:AllowInsecureHttp");
        if (allowInsecureHttp
            && !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ServiceIdentity:AllowInsecureHttp is permitted only in Development or Test environments.");
        }

        var boltTransportTokenIssuerEnabled = configuration.GetValue<bool>(
            "ServiceIdentity:BoltTransportTokenIssuer:Enabled");
        var authorityValue = configuration["ServiceIdentity:Authority"]?.Trim();
        Uri? boltTransportDiscoveryAuthority = null;
        if (!string.IsNullOrWhiteSpace(authorityValue))
        {
            if (!Uri.TryCreate(authorityValue, UriKind.Absolute, out var authority) ||
                (authority.Scheme != Uri.UriSchemeHttps && authority.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(authority.UserInfo) ||
                !string.IsNullOrEmpty(authority.Query) ||
                !string.IsNullOrEmpty(authority.Fragment) ||
                authority.AbsolutePath is not ("" or "/"))
            {
                throw new InvalidOperationException(
                    "ServiceIdentity:Authority must be an absolute HTTP or HTTPS origin.");
            }

            if (authority.Scheme == Uri.UriSchemeHttp && !allowInsecureHttp)
            {
                throw new InvalidOperationException(
                    "ServiceIdentity:Authority must use HTTPS unless ServiceIdentity:AllowInsecureHttp is explicitly true.");
            }

            boltTransportDiscoveryAuthority = authority;
        }

        if (boltTransportTokenIssuerEnabled && boltTransportDiscoveryAuthority is null)
        {
            throw new InvalidOperationException(
                "ServiceIdentity:Authority is required when Bolt transport token issuance is enabled.");
        }
        var boltTransportTokenLifetimeSeconds = configuration.GetValue(
            "ServiceIdentity:BoltTransportTokenIssuer:LifetimeSeconds",
            DefaultBoltTransportTokenLifetimeSeconds);
        if (boltTransportTokenLifetimeSeconds is < MinimumBoltTransportTokenLifetimeSeconds
            or > MaximumBoltTransportTokenLifetimeSeconds)
        {
            throw new InvalidOperationException(
                $"ServiceIdentity:BoltTransportTokenIssuer:LifetimeSeconds must be between " +
                $"{MinimumBoltTransportTokenLifetimeSeconds} and {MaximumBoltTransportTokenLifetimeSeconds} seconds.");
        }

        var boltTransportSigningKeyPath = configuration[
            "ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath"]?.Trim();
        if (boltTransportTokenIssuerEnabled && string.IsNullOrWhiteSpace(boltTransportSigningKeyPath))
        {
            throw new InvalidOperationException(
                "ServiceIdentity:BoltTransportTokenIssuer:SigningKeyPath is required when Bolt transport token issuance is enabled.");
        }

        if (!string.IsNullOrWhiteSpace(boltTransportSigningKeyPath))
            boltTransportSigningKeyPath = Path.GetFullPath(boltTransportSigningKeyPath);

        return new ServiceIdentityConfiguration(
            issuer,
            Math.Clamp(configuration.GetValue("ServiceIdentity:TokenLifetimeMinutes", 10), 1, 60),
            allowInsecureHttp,
            boltTransportDiscoveryAuthority,
            boltTransportTokenIssuerEnabled,
            boltTransportTokenLifetimeSeconds,
            boltTransportSigningKeyPath,
            clients);
    }
}

internal sealed class ServiceClientDefinition
{
    private readonly CredentialGenerationDescriptor _current;
    private readonly CredentialGenerationDescriptor? _validationFallback;

    private ServiceClientDefinition(
        string clientId,
        CredentialGenerationDescriptor current,
        CredentialGenerationDescriptor? validationFallback,
        HashSet<string> allowedAudiences,
        HashSet<string> allowedScopes)
    {
        ClientId = clientId;
        _current = current;
        _validationFallback = validationFallback;
        AllowedAudiences = allowedAudiences;
        AllowedScopes = allowedScopes;
        ValidationGenerationIds = validationFallback is null
            ? [current.GenerationId]
            : [current.GenerationId, validationFallback.Value.GenerationId];
    }

    public string ClientId { get; }
    public HashSet<string> AllowedAudiences { get; }
    public HashSet<string> AllowedScopes { get; }
    public IReadOnlyList<string> ValidationGenerationIds { get; }

    public bool TryAuthenticate(string? suppliedSecret, DateTimeOffset nowUtc, out string? generationId)
    {
        if (CredentialGenerationValidator.FixedTimeEquals(_current.Secret, suppliedSecret))
        {
            generationId = _current.GenerationId;
            return true;
        }

        if (_validationFallback is { } fallback
            && CredentialGenerationValidator.IsActive(fallback, nowUtc)
            && CredentialGenerationValidator.FixedTimeEquals(fallback.Secret, suppliedSecret))
        {
            generationId = fallback.GenerationId;
            return true;
        }

        generationId = null;
        return false;
    }

    public static ServiceClientDefinition FromConfiguration(IConfigurationSection section, DateTimeOffset nowUtc)
    {
        var clientId = section["ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException($"{section.Path}:ClientId is required.");

        var current = new CredentialGenerationDescriptor(
            section["GenerationId"] ?? string.Empty,
            section["ClientSecret"] ?? string.Empty);

        var fallbackSection = section.GetSection("ValidationFallback");
        var fallbackGenerationId = fallbackSection["GenerationId"];
        var fallbackSecret = fallbackSection["ClientSecret"];
        var fallbackValidUntilUtc = fallbackSection.GetValue<DateTimeOffset?>("ValidUntilUtc");
        var hasFallback = !string.IsNullOrWhiteSpace(fallbackGenerationId)
            || !string.IsNullOrWhiteSpace(fallbackSecret)
            || fallbackValidUntilUtc.HasValue;
        CredentialGenerationDescriptor? fallback = hasFallback
            ? new CredentialGenerationDescriptor(
                fallbackGenerationId ?? string.Empty,
                fallbackSecret ?? string.Empty,
                fallbackValidUntilUtc)
            : null;

        CredentialGenerationValidator.Validate(section.Path, current, fallback, nowUtc);

        var audiences = ParseList(section, "AllowedAudiences", StringComparer.Ordinal);
        var scopes = ParseList(section, "AllowedScopes", StringComparer.OrdinalIgnoreCase);
        if (audiences.Count == 0)
            throw new InvalidOperationException($"{section.Path}:AllowedAudiences must contain at least one audience.");
        if (scopes.Count == 0)
            throw new InvalidOperationException($"{section.Path}:AllowedScopes must contain at least one scope.");

        return new ServiceClientDefinition(
            clientId,
            current with { GenerationId = current.GenerationId.Trim() },
            fallback is null
                ? null
                : fallback.Value with { GenerationId = fallback.Value.GenerationId.Trim() },
            audiences,
            scopes);
    }

    private static HashSet<string> ParseList(
        IConfigurationSection section,
        string key,
        StringComparer comparer)
    {
        var values = section.GetSection(key)
            .GetChildren()
            .Select(static child => child.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToList();

        if (values.Count == 0 && section[key] is { } scalar)
        {
            values.AddRange(scalar.Split(
                [',', ';', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return values.ToHashSet(comparer);
    }
}
