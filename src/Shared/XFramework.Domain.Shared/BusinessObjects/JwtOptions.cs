namespace XFramework.Domain.Shared.BusinessObjects;

public class JwtOptions
{
    public string GenerationId { get; set; } = string.Empty;
    public string SigningPublicKeyPath { get; set; } = string.Empty;
    public string? SigningPrivateKeyPath { get; set; }
    public JwtValidationFallbackOptions? ValidationFallback { get; set; }
    public string ValidIssuer { get; set; } = string.Empty;
    public string ValidAudience { get; set; } = string.Empty;
    public string AccessTokenLifespan { get; set; } = string.Empty;
    public string RefreshTokenLifespan { get; set; } = string.Empty;

    public bool HasValidationFallback => ValidationFallback is { } fallback
        && (!string.IsNullOrWhiteSpace(fallback.GenerationId)
            || !string.IsNullOrWhiteSpace(fallback.SigningPublicKeyPath)
            || fallback.ValidUntilUtc.HasValue);

    public IReadOnlyList<string> ValidationGenerationIds => !HasValidationFallback
        ? [GenerationId]
        : [GenerationId, ValidationFallback!.GenerationId];
}

public sealed class JwtValidationFallbackOptions
{
    public string GenerationId { get; set; } = string.Empty;
    public string SigningPublicKeyPath { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntilUtc { get; set; }
}
