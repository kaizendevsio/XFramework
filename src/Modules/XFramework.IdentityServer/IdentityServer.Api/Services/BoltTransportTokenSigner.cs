using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.Api.Services;

public static class BoltTransportTokenConstants
{
    public const string Algorithm = SecurityAlgorithms.RsaSha256;
    public const string Audience = XFrameworkServiceNames.BoltHub;
    public const string Scope = XFrameworkServiceScopes.BoltService;
    public const string TokenType = "bolt+jwt";
    public const string MetadataPath = "/.well-known/openid-configuration";
    public const string JsonWebKeySetPath = "/.well-known/bolt-transport-jwks.json";
    public const string TokenEndpointPath = "/api/service-identity/bolt-transport-token";
}

public interface IBoltTransportTokenSigner
{
    string KeyId { get; }

    string Sign(
        string clientId,
        string clientCredentialGenerationId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc);

    BoltTransportJsonWebKeySet GetJsonWebKeySet();
}

public sealed class FileBackedBoltTransportTokenSigner : IBoltTransportTokenSigner
{
    private const int KeySizeBits = 3072;
    private static readonly JwtSecurityTokenHandler TokenHandler = new();
    private readonly SigningCredentials? _signingCredentials;
    private readonly BoltTransportJsonWebKey? _publicKey;
    private readonly ServiceIdentityConfiguration _configuration;

    public FileBackedBoltTransportTokenSigner(ServiceIdentityConfiguration configuration)
    {
        _configuration = configuration;
        var signingKeyPath = configuration.BoltTransportSigningKeyPath;
        if (string.IsNullOrWhiteSpace(signingKeyPath))
            return;

        var privateKeyParameters = LoadOrCreatePrivateKey(signingKeyPath);
        using var rsa = RSA.Create();
        rsa.ImportParameters(privateKeyParameters);
        var publicParameters = rsa.ExportParameters(includePrivateParameters: false);

        KeyId = $"bolt-{Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))}";
        _signingCredentials = new SigningCredentials(
            new RsaSecurityKey(privateKeyParameters) { KeyId = KeyId },
            BoltTransportTokenConstants.Algorithm);
        _publicKey = new BoltTransportJsonWebKey
        {
            KeyType = "RSA",
            Use = "sig",
            KeyId = KeyId,
            Algorithm = BoltTransportTokenConstants.Algorithm,
            Modulus = Base64UrlEncoder.Encode(publicParameters.Modulus!),
            Exponent = Base64UrlEncoder.Encode(publicParameters.Exponent!)
        };
    }

    public string KeyId { get; } = string.Empty;

    public string Sign(
        string clientId,
        string clientCredentialGenerationId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientCredentialGenerationId);
        if (expiresAtUtc <= issuedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Token expiry must be after issuance.");
        if (_signingCredentials is null)
        {
            throw new InvalidOperationException(
                "Bolt transport signing is unavailable because no signing key path is configured.");
        }

        List<Claim> claims =
        [
            new("client_id", clientId),
            new("service", clientId),
            new(JwtRegisteredClaimNames.Sub, clientId),
            new("scope", BoltTransportTokenConstants.Scope),
            new("client_credential_generation", clientCredentialGenerationId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAtUtc.UtcDateTime).ToString(),
                ClaimValueTypes.Integer64)
        ];

        var token = new JwtSecurityToken(
            issuer: _configuration.Issuer,
            audience: BoltTransportTokenConstants.Audience,
            claims: claims,
            notBefore: issuedAtUtc.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: _signingCredentials);
        token.Header["typ"] = BoltTransportTokenConstants.TokenType;

        return TokenHandler.WriteToken(token);
    }

    public BoltTransportJsonWebKeySet GetJsonWebKeySet() => new()
    {
        Keys = _publicKey is null ? [] : [_publicKey]
    };

    private static RSAParameters LoadOrCreatePrivateKey(string signingKeyPath)
    {
        var directoryPath = Path.GetDirectoryName(signingKeyPath)
            ?? throw new InvalidOperationException("Bolt transport signing key path must include a directory.");
        Directory.CreateDirectory(directoryPath);

        if (!File.Exists(signingKeyPath))
            CreatePrivateKeyAtomically(signingKeyPath, directoryPath);

        RestrictUnixFilePermissions(signingKeyPath);
        var privateKeyPem = File.ReadAllText(signingKeyPath, Encoding.ASCII);
        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(privateKeyPem);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Bolt transport signing key is not a valid RSA PEM private key.", exception);
        }

        if (rsa.KeySize != KeySizeBits)
        {
            throw new InvalidOperationException(
                $"Bolt transport signing key must be RSA-{KeySizeBits}; configured key is RSA-{rsa.KeySize}.");
        }

        return rsa.ExportParameters(includePrivateParameters: true);
    }

    private static void CreatePrivateKeyAtomically(string signingKeyPath, string directoryPath)
    {
        using var rsa = RSA.Create(KeySizeBits);
        var privateKeyBytes = Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem());
        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(signingKeyPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var streamOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                streamOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            using (var stream = new FileStream(temporaryPath, streamOptions))
            {
                stream.Write(privateKeyBytes);
                stream.Flush(flushToDisk: true);
            }

            RestrictUnixFilePermissions(temporaryPath);
            try
            {
                File.Move(temporaryPath, signingKeyPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(signingKeyPath))
            {
                // Another process won the atomic create. Its complete key is loaded below.
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void RestrictUnixFilePermissions(string path)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

public sealed record BoltTransportJsonWebKeySet
{
    [JsonPropertyName("keys")]
    public required IReadOnlyList<BoltTransportJsonWebKey> Keys { get; init; }
}

public sealed record BoltTransportJsonWebKey
{
    [JsonPropertyName("kty")]
    public required string KeyType { get; init; }

    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("kid")]
    public required string KeyId { get; init; }

    [JsonPropertyName("alg")]
    public required string Algorithm { get; init; }

    [JsonPropertyName("n")]
    public required string Modulus { get; init; }

    [JsonPropertyName("e")]
    public required string Exponent { get; init; }
}
