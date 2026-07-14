using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public sealed class IdentityServerServiceTokenProvider : IServiceTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceIdentityOptions> _serviceIdentityOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityServerTokenCache _tokenCache;

    public IdentityServerServiceTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceIdentityOptions> serviceIdentityOptions,
        TimeProvider timeProvider,
        ILogger<IdentityServerServiceTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceIdentityOptions = serviceIdentityOptions;
        _timeProvider = timeProvider;
        _tokenCache = new IdentityServerTokenCache(serviceIdentityOptions, timeProvider, logger);
    }

    public ValueTask<string> GetTokenAsync(
        string audience,
        IReadOnlyCollection<string>? scopes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Service token audience is required.");

        var requestedScopes = NormalizeScopes(scopes);
        var normalizedAudience = audience.Trim();
        var cacheKey = $"{normalizedAudience}|{string.Join(' ', requestedScopes)}";

        return _tokenCache.GetTokenAsync(
            cacheKey,
            "service",
            acquisitionCt => AcquireTokenAsync(normalizedAudience, requestedScopes, acquisitionCt),
            ct);
    }

    private Task<ServiceTokenResponse> AcquireTokenAsync(
        string audience,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken ct)
    {
        var options = _serviceIdentityOptions.Value;
        var clientId = ResolveClientId(options);
        var request = CreateCurrentCredentialRequest(
            options,
            clientId,
            audience,
            requestedScopes,
            _timeProvider.GetUtcNow());

        return ServiceIdentityHttpClient.PostForTokenAsync(
            _httpClientFactory,
            options,
            ServiceIdentityHttpClient.ServiceTokenPath,
            request,
            ct);
    }

    private static string ResolveClientId(ServiceIdentityOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientId))
            return options.ClientId.Trim();

        throw new InvalidOperationException("ServiceIdentity:ClientId is required.");
    }

    private List<string> NormalizeScopes(IReadOnlyCollection<string>? scopes)
    {
        var defaults = _serviceIdentityOptions.Value.DefaultScopes;
        var source = scopes is { Count: > 0 } ? scopes : defaults;

        return source
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(static scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IssueServiceTokenRequest CreateCurrentCredentialRequest(
        ServiceIdentityOptions options,
        string clientId,
        string audience,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset nowUtc)
    {
        CredentialGenerationValidator.Validate(
            ServiceIdentityOptions.SectionName,
            new CredentialGenerationDescriptor(
                options.GenerationId ?? string.Empty,
                options.ClientSecret ?? string.Empty),
            validationFallback: null,
            nowUtc);

        return new IssueServiceTokenRequest
        {
            ClientId = clientId,
            ClientSecret = options.ClientSecret!,
            Audience = audience,
            Scopes = scopes.ToList(),
            Metadata = new RequestMetadata
            {
                Name = clientId,
                RequestId = Guid.NewGuid()
            }
        };
    }
}
