using System.Security.Cryptography;
using System.Text;

namespace XFramework.Integration.Security;

public static class CredentialGenerationValidator
{
    public const int MinimumSecretBytes = 32;
    public static readonly TimeSpan MaximumValidationFallbackLifetime = TimeSpan.FromHours(8);

    public static void Validate(
        string configurationPath,
        CredentialGenerationDescriptor current,
        CredentialGenerationDescriptor? validationFallback,
        DateTimeOffset nowUtc,
        int minimumSecretBytes = MinimumSecretBytes)
    {
        ValidateGeneration(configurationPath, current, requireBound: false, minimumSecretBytes);

        if (validationFallback is not { } fallback)
            return;

        ValidateGeneration($"{configurationPath}:ValidationFallback", fallback, requireBound: true, minimumSecretBytes);

        if (string.Equals(current.GenerationId.Trim(), fallback.GenerationId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{configurationPath} current and validation fallback GenerationId values must be distinct.");
        }

        if (FixedTimeEquals(current.Secret, fallback.Secret))
        {
            throw new InvalidOperationException(
                $"{configurationPath} current and validation fallback secrets must be distinct.");
        }

        if (fallback.ValidUntilUtc <= nowUtc)
        {
            throw new InvalidOperationException(
                $"{configurationPath}:ValidationFallback:ValidUntilUtc must be in the future at startup.");
        }

        if (fallback.ValidUntilUtc > nowUtc.Add(MaximumValidationFallbackLifetime))
        {
            throw new InvalidOperationException(
                $"{configurationPath}:ValidationFallback:ValidUntilUtc must be no more than " +
                $"{MaximumValidationFallbackLifetime.TotalHours:0} hours in the future at startup.");
        }
    }

    public static bool FixedTimeEquals(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public static bool IsActive(CredentialGenerationDescriptor generation, DateTimeOffset nowUtc) =>
        generation.ValidUntilUtc is null || generation.ValidUntilUtc > nowUtc;

    private static void ValidateGeneration(
        string configurationPath,
        CredentialGenerationDescriptor generation,
        bool requireBound,
        int minimumSecretBytes)
    {
        if (string.IsNullOrWhiteSpace(generation.GenerationId))
            throw new InvalidOperationException($"{configurationPath}:GenerationId is required.");

        var generationId = generation.GenerationId.Trim();
        if (generationId.Length > 128 || generationId.Any(char.IsWhiteSpace) || generationId.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{configurationPath}:GenerationId must be at most 128 characters and contain no whitespace or control characters.");
        }

        if (string.IsNullOrWhiteSpace(generation.Secret) || Encoding.UTF8.GetByteCount(generation.Secret) < minimumSecretBytes)
        {
            throw new InvalidOperationException(
                $"{configurationPath} secret must contain at least {minimumSecretBytes} UTF-8 bytes.");
        }

        if (!requireBound)
            return;

        if (generation.ValidUntilUtc is null)
        {
            throw new InvalidOperationException(
                $"{configurationPath}:ValidUntilUtc is required for validation-only credentials.");
        }

        if (generation.ValidUntilUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{configurationPath}:ValidUntilUtc must use the UTC offset (Z or +00:00).");
        }
    }
}

public readonly record struct CredentialGenerationDescriptor(
    string GenerationId,
    string Secret,
    DateTimeOffset? ValidUntilUtc = null);
