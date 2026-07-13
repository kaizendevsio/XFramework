using Microsoft.Extensions.Options;

namespace XFramework.Integration.Security;

public sealed class ServiceIdentityOptions
{
    public const string SectionName = "ServiceIdentity";

    public string? ClientId { get; set; }
    public string? GenerationId { get; set; }
    public string? ClientSecret { get; set; }
    public ServiceIdentityValidationFallbackOptions? ValidationFallback { get; set; }
    public string Issuer { get; set; } = "XFramework.IdentityServer";
    public int TokenRefreshSkewSeconds { get; set; } = 60;
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
            options.ValidateClientCredential(timeProvider.GetUtcNow());
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
