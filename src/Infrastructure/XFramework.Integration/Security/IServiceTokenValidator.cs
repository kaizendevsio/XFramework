namespace XFramework.Integration.Security;

public interface IServiceTokenValidator
{
    Task<ServiceTokenValidationResult> ValidateAsync(
        string? token,
        string expectedAudience,
        IReadOnlyCollection<string>? requiredScopes = null,
        CancellationToken ct = default);
}
