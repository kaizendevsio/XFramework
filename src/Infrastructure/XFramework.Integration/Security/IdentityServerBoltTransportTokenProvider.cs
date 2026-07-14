using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XFramework.Integration.Security;

public sealed class IdentityServerBoltTransportTokenProvider : IBoltTransportTokenProvider
{
    private const string CacheKey = "bolt-transport";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ServiceIdentityOptions> _serviceIdentityOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IdentityServerTokenCache _tokenCache;

    public IdentityServerBoltTransportTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ServiceIdentityOptions> serviceIdentityOptions,
        TimeProvider timeProvider,
        ILogger<IdentityServerBoltTransportTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serviceIdentityOptions = serviceIdentityOptions;
        _timeProvider = timeProvider;
        _tokenCache = new IdentityServerTokenCache(serviceIdentityOptions, timeProvider, logger);
    }

    public ValueTask<string> GetTokenAsync(CancellationToken ct = default) =>
        _tokenCache.GetTokenAsync(CacheKey, "Bolt transport", AcquireTokenAsync, ct);

    private Task<XFramework.Domain.Shared.ServiceIdentity.ServiceTokenResponse> AcquireTokenAsync(CancellationToken ct)
    {
        var options = _serviceIdentityOptions.Value;
        var clientId = ResolveClientId(options);
        ValidateCurrentCredential(options);

        return ServiceIdentityHttpClient.PostForTokenAsync(
            _httpClientFactory,
            options,
            ServiceIdentityHttpClient.BoltTransportTokenPath,
            new BoltTransportTokenRequest(clientId, options.ClientSecret!),
            ct);
    }

    private static string ResolveClientId(ServiceIdentityOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientId))
            return options.ClientId.Trim();

        throw new InvalidOperationException("ServiceIdentity:ClientId is required.");
    }

    private void ValidateCurrentCredential(ServiceIdentityOptions options) =>
        CredentialGenerationValidator.Validate(
            ServiceIdentityOptions.SectionName,
            new CredentialGenerationDescriptor(
                options.GenerationId ?? string.Empty,
                options.ClientSecret ?? string.Empty),
            validationFallback: null,
            _timeProvider.GetUtcNow());

    private sealed record BoltTransportTokenRequest(string ClientId, string ClientSecret);
}
