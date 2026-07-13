using System.Security.Cryptography;
using System.Text;

namespace XFramework.Bolt.Phase0Synthetics;

public sealed class SecretToken
{
    private const int EvidencePrefixLength = 12;
    private readonly string _value;
    private readonly byte[] _sha256;

    public SecretToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new SyntheticConfigurationException("missing_token_value");

        _value = value;
        _sha256 = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Sha256Prefix = Convert.ToHexString(_sha256)[..EvidencePrefixLength].ToLowerInvariant();
    }

    public string Sha256Prefix { get; }

    internal string Reveal() => _value;

    internal bool HasSameValue(SecretToken other) =>
        CryptographicOperations.FixedTimeEquals(_sha256, other._sha256);

    public override string ToString() => "[REDACTED]";
}
