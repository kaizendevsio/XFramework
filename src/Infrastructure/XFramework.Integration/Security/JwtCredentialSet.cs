using System.Security.Claims;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Integration.Security;

public static class JwtCredentialSet
{
    public const string GenerationClaim = "credential_generation";
    public const int MinimumRsaKeySize = 2048;
    private static readonly object KeyCreationLock = new();
    private static readonly ConcurrentDictionary<string, Lazy<RsaSecurityKey>> PublicKeys = new();
    private static readonly ConcurrentDictionary<string, Lazy<RsaSecurityKey>> PrivateKeys = new();

    public static void Validate(
        JwtOptions options,
        DateTimeOffset nowUtc,
        string environmentName = "Production")
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ValidIssuer))
            throw new InvalidOperationException("JwtOptions:ValidIssuer is required.");
        if (string.IsNullOrWhiteSpace(options.ValidAudience))
            throw new InvalidOperationException("JwtOptions:ValidAudience is required.");
        if (string.IsNullOrWhiteSpace(options.GenerationId))
            throw new InvalidOperationException("JwtOptions:GenerationId is required.");
        if (string.IsNullOrWhiteSpace(options.SigningPublicKeyPath))
            throw new InvalidOperationException("JwtOptions:SigningPublicKeyPath is required.");

        EnsureSigningKeyPair(options, environmentName);
        var publicKey = LoadPublicKey(options.SigningPublicKeyPath, options.GenerationId);
        if (!string.IsNullOrWhiteSpace(options.SigningPrivateKeyPath))
            ValidateKeyPair(options.SigningPrivateKeyPath, publicKey.Rsa);

        if (!options.HasValidationFallback)
            return;

        var fallback = options.ValidationFallback!;
        if (string.IsNullOrWhiteSpace(fallback.GenerationId) ||
            string.IsNullOrWhiteSpace(fallback.SigningPublicKeyPath) ||
            fallback.ValidUntilUtc is null)
        {
            throw new InvalidOperationException(
                "JwtOptions:ValidationFallback requires GenerationId, SigningPublicKeyPath, and ValidUntilUtc.");
        }

        if (fallback.ValidUntilUtc <= nowUtc)
            throw new InvalidOperationException("JwtOptions:ValidationFallback has expired.");
        if (string.Equals(fallback.GenerationId.Trim(), options.GenerationId.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("JWT signing generations must use distinct IDs.");

        _ = LoadPublicKey(fallback.SigningPublicKeyPath, fallback.GenerationId);
    }

    public static RsaSecurityKey CreateCurrentSigningKey(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningPrivateKeyPath))
            throw new InvalidOperationException(
                "JWT token generation is restricted to IdentityServer and requires JwtOptions:SigningPrivateKeyPath.");

        var path = Path.GetFullPath(options.SigningPrivateKeyPath);
        var generationId = options.GenerationId.Trim();
        return PrivateKeys.GetOrAdd(
            $"{path}|{generationId}",
            _ => new Lazy<RsaSecurityKey>(
                () => LoadPrivateKey(path, generationId),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public static TokenValidationParameters CreateValidationParameters(
        JwtOptions options,
        bool validateLifetime,
        TimeProvider? timeProvider = null)
    {
        var clock = timeProvider ?? TimeProvider.System;
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, keyId, _) => ResolveValidationKeys(options, keyId, clock.GetUtcNow()),
            IssuerSigningKeyValidator = ValidateGenerationClaim,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = options.ValidAudience,
            ValidIssuer = options.ValidIssuer,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha512]
        };

        if (!validateLifetime)
        {
            parameters.LifetimeValidator = (notBefore, expires, _, validationParameters) =>
                ValidateRefreshLifetime(notBefore, expires, clock.GetUtcNow().UtcDateTime, validationParameters.ClockSkew);
        }

        return parameters;
    }

    public static IReadOnlyList<SecurityKey> ResolveValidationKeys(
        JwtOptions options,
        string? keyId,
        DateTimeOffset nowUtc)
    {
        List<SecurityKey> keys = [LoadPublicKey(options.SigningPublicKeyPath, options.GenerationId)];
        var fallback = options.ValidationFallback;
        if (fallback is { ValidUntilUtc: { } validUntil } && validUntil > nowUtc)
            keys.Add(LoadPublicKey(fallback.SigningPublicKeyPath, fallback.GenerationId));

        return string.IsNullOrWhiteSpace(keyId)
            ? keys
            : keys.Where(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal)).ToList();
    }

    private static void EnsureSigningKeyPair(JwtOptions options, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(options.SigningPrivateKeyPath))
            return;

        lock (KeyCreationLock)
        {
            var privatePath = Path.GetFullPath(options.SigningPrivateKeyPath);
            var publicPath = Path.GetFullPath(options.SigningPublicKeyPath);
            if (File.Exists(privatePath) && File.Exists(publicPath))
                return;

            if (File.Exists(privatePath) != File.Exists(publicPath))
                throw new InvalidOperationException("JWT signing key pair is incomplete.");

            if (!CanGenerateSigningKeys(environmentName))
            {
                throw new InvalidOperationException(
                    "JWT signing key files must be provisioned outside Development or Test environments.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
            using var rsa = RSA.Create(3072);
            WriteKeyAtomically(privatePath, rsa.ExportPkcs8PrivateKeyPem(), privateKey: true);
            WriteKeyAtomically(publicPath, rsa.ExportSubjectPublicKeyInfoPem(), privateKey: false);
        }
    }

    private static void WriteKeyAtomically(string path, string pem, bool privateKey)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = privateKey
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            }

            using (var stream = new FileStream(temporaryPath, options))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(pem);
            }

            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool CanGenerateSigningKeys(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase);

    private static RsaSecurityKey LoadPublicKey(string path, string generationId)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"JWT signing public key was not found at '{path}'.");

        var fullPath = Path.GetFullPath(path);
        var normalizedGenerationId = generationId.Trim();
        return PublicKeys.GetOrAdd(
            $"{fullPath}|{normalizedGenerationId}",
            _ => new Lazy<RsaSecurityKey>(
                () => LoadPublicKeyCore(fullPath, normalizedGenerationId),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static RsaSecurityKey LoadPublicKeyCore(string path, string generationId)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(path));
            EnsureMinimumKeySize(rsa);
            return new RsaSecurityKey(rsa) { KeyId = generationId };
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static RsaSecurityKey LoadPrivateKey(string path, string generationId)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"JWT signing private key was not found at '{path}'.");

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(path));
            EnsureMinimumKeySize(rsa);
            return new RsaSecurityKey(rsa) { KeyId = generationId };
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static void ValidateKeyPair(string privateKeyPath, RSA publicKey)
    {
        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(File.ReadAllText(privateKeyPath));
        EnsureMinimumKeySize(privateKey);
        var expected = publicKey.ExportParameters(includePrivateParameters: false);
        var actual = privateKey.ExportParameters(includePrivateParameters: false);
        if (!CryptographicOperations.FixedTimeEquals(expected.Modulus!, actual.Modulus!) ||
            !CryptographicOperations.FixedTimeEquals(expected.Exponent!, actual.Exponent!))
        {
            throw new InvalidOperationException("JWT signing public and private keys do not form a key pair.");
        }
    }

    private static void EnsureMinimumKeySize(RSA rsa)
    {
        if (rsa.KeySize < MinimumRsaKeySize)
            throw new InvalidOperationException(
                $"JWT signing RSA keys must be at least {MinimumRsaKeySize} bits.");
    }

    private static bool ValidateGenerationClaim(
        SecurityKey signingKey,
        SecurityToken token,
        TokenValidationParameters _)
    {
        IEnumerable<Claim> claims = token switch
        {
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwtToken => jwtToken.Claims,
            JsonWebToken jsonWebToken => jsonWebToken.Claims,
            _ => []
        };
        var generations = claims
            .Where(static claim => claim.Type == GenerationClaim)
            .Select(static claim => claim.Value)
            .ToList();

        return generations.Count == 1 &&
               !string.IsNullOrWhiteSpace(signingKey.KeyId) &&
               string.Equals(generations[0], signingKey.KeyId, StringComparison.Ordinal);
    }

    private static bool ValidateRefreshLifetime(
        DateTime? notBefore,
        DateTime? expires,
        DateTime nowUtc,
        TimeSpan clockSkew)
    {
        if (expires is null || notBefore > expires)
            return false;

        return notBefore is null || notBefore <= nowUtc.Add(clockSkew);
    }
}
