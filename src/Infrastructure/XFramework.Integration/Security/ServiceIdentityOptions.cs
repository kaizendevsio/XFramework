using Microsoft.Extensions.Options;

namespace XFramework.Integration.Security;

public sealed class ServiceIdentityOptions
{
    public const string SectionName = "ServiceIdentity";

    public string? Authority { get; set; }
    public bool AllowInsecureHttp { get; set; }
    public string? ClientId { get; set; }
    public string? GenerationId { get; set; }
    public string? ClientSecret { get; set; }
    public ServiceIdentityValidationFallbackOptions? ValidationFallback { get; set; }
    public string Issuer { get; set; } = "XFramework.IdentityServer";
    public int TokenRefreshSkewSeconds { get; set; } = 60;
    public int TokenAcquisitionTimeoutSeconds { get; set; } = 30;
    public int SigningKeyCacheMinutes { get; set; } = 15;
    public List<string> DefaultScopes { get; set; } = [];

    public bool HasValidationFallback => ValidationFallback is { } fallback
        && (!string.IsNullOrWhiteSpace(fallback.GenerationId)
            || !string.IsNullOrWhiteSpace(fallback.ClientSecret)
            || fallback.ValidUntilUtc.HasValue);

    public IReadOnlyList<string> ValidationGenerationIds => string.IsNullOrWhiteSpace(GenerationId)
        ? []
        : !HasValidationFallback
            ? [GenerationId]
            : [GenerationId, ValidationFallback!.GenerationId];

    public void ValidateClientCredential(DateTimeOffset nowUtc)
    {
        var fallback = HasValidationFallback
            ? new CredentialGenerationDescriptor(
                ValidationFallback!.GenerationId,
                ValidationFallback.ClientSecret,
                ValidationFallback.ValidUntilUtc)
            : (CredentialGenerationDescriptor?)null;

        CredentialGenerationValidator.Validate(
            SectionName,
            new CredentialGenerationDescriptor(GenerationId ?? string.Empty, ClientSecret ?? string.Empty),
            fallback,
            nowUtc);
    }

    public Uri ResolveAuthority()
    {
        if (string.IsNullOrWhiteSpace(Authority) ||
            !Uri.TryCreate(Authority.Trim(), UriKind.Absolute, out var authority))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Authority must be a valid absolute HTTP or HTTPS URI.");
        }

        if (!string.IsNullOrEmpty(authority.UserInfo) ||
            !string.IsNullOrEmpty(authority.Query) ||
            !string.IsNullOrEmpty(authority.Fragment) ||
            authority.AbsolutePath is not ("" or "/"))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Authority must be an origin without user information, a path, a query, or a fragment.");
        }

        if (string.Equals(authority.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return authority;

        if (string.Equals(authority.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            AllowInsecureHttp)
        {
            return authority;
        }

        if (string.Equals(authority.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Authority must use HTTPS unless {SectionName}:AllowInsecureHttp is explicitly true.");
        }

        throw new InvalidOperationException(
            $"{SectionName}:Authority must use the HTTP or HTTPS scheme.");
    }
}

public sealed class ServiceIdentityValidationFallbackOptions
{
    public string GenerationId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntilUtc { get; set; }
}

public sealed class ServiceIdentityOptionsValidator(TimeProvider timeProvider)
    : IValidateOptions<ServiceIdentityOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceIdentityOptions options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
                return ValidateOptionsResult.Fail($"{ServiceIdentityOptions.SectionName}:ClientId is required.");

            options.ResolveAuthority();
            options.ValidateClientCredential(timeProvider.GetUtcNow());
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
