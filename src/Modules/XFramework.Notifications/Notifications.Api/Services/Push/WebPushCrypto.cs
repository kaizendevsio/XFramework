using System.Security.Cryptography;
using System.Text;

namespace Notifications.Api.Services.Push;

/// <summary>
/// RFC 8291 "Message Encryption for Web Push" over the RFC 8188 aes128gcm content coding.
/// Implemented on BCL primitives so no third-party crypto enters the delivery path; the
/// RFC 8291 section 5 vector is asserted in Notifications.Tests because a subtly wrong
/// derivation still produces a well-formed body that silently never decrypts on device.
/// </summary>
public static class WebPushCrypto
{
    /// <summary>Record size advertised in the aes128gcm header. Push payloads here are a few hundred bytes.</summary>
    public const int RecordSize = 4096;

    /// <summary>Largest plaintext that fits one record: the 0x02 delimiter plus the GCM tag take 17 bytes.</summary>
    public const int MaxPlaintextBytes = RecordSize - 17;

    private const int UncompressedPointLength = 65;

    private static readonly byte[] KeyInfoPrefix = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] CekInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] NonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>Encrypts a push payload for one subscription. Salt and sender key are freshly random per message.</summary>
    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, byte[] receiverPublicKey, byte[] authSecret)
    {
        using var senderKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(plaintext, receiverPublicKey, authSecret, RandomNumberGenerator.GetBytes(16), senderKey);
    }

    /// <summary>Deterministic overload: the salt and sender key are supplied so the RFC vector can be reproduced.</summary>
    internal static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        byte[] receiverPublicKey,
        byte[] authSecret,
        byte[] salt,
        ECDiffieHellman senderKey)
    {
        ArgumentNullException.ThrowIfNull(receiverPublicKey);
        ArgumentNullException.ThrowIfNull(authSecret);
        if (receiverPublicKey.Length != UncompressedPointLength || receiverPublicKey[0] != 0x04)
            throw new ArgumentException("p256dh must be a 65-byte uncompressed P-256 point.", nameof(receiverPublicKey));
        if (authSecret.Length != 16)
            throw new ArgumentException("The auth secret must be 16 bytes.", nameof(authSecret));
        if (salt.Length != 16)
            throw new ArgumentException("The salt must be 16 bytes.", nameof(salt));
        if (plaintext.Length > MaxPlaintextBytes)
            throw new ArgumentException($"Push payloads must stay under {MaxPlaintextBytes} bytes.", nameof(plaintext));

        var senderPublicKey = ExportUncompressedPoint(senderKey);
        using var receiver = ImportPublicKey(receiverPublicKey);

        // HKDF-Extract(salt = auth_secret, IKM = ECDH shared secret), then expand with the
        // WebPush key info that binds both public keys into the derived keying material.
        var ecdhSecret = senderKey.DeriveRawSecretAgreement(receiver.PublicKey);
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, authSecret);
        var keyInfo = Concat(KeyInfoPrefix, receiverPublicKey, senderPublicKey);
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, keyInfo);

        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, CekInfo);
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, NonceInfo);

        // Single record, so the padding delimiter is 0x02 (last record) and the sequence
        // number is zero, which leaves the derived nonce unchanged.
        var record = new byte[plaintext.Length + 1];
        plaintext.CopyTo(record);
        record[^1] = 0x02;

        var ciphertext = new byte[record.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, 16))
            aes.Encrypt(nonce, record, ciphertext, tag);

        CryptographicOperations.ZeroMemory(ecdhSecret);
        CryptographicOperations.ZeroMemory(prkKey);
        CryptographicOperations.ZeroMemory(ikm);
        CryptographicOperations.ZeroMemory(prk);
        CryptographicOperations.ZeroMemory(cek);
        CryptographicOperations.ZeroMemory(record);

        // aes128gcm header: salt(16) || record size(4, big endian) || key id length(1) || key id.
        var body = new byte[16 + 4 + 1 + UncompressedPointLength + ciphertext.Length + tag.Length];
        var offset = 0;
        salt.CopyTo(body, offset);
        offset += 16;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(offset), RecordSize);
        offset += 4;
        body[offset++] = UncompressedPointLength;
        senderPublicKey.CopyTo(body, offset);
        offset += UncompressedPointLength;
        ciphertext.CopyTo(body, offset);
        offset += ciphertext.Length;
        tag.CopyTo(body, offset);
        return body;
    }

    /// <summary>Exports the 65-byte uncompressed point a push service expects as the aes128gcm key id.</summary>
    public static byte[] ExportUncompressedPoint(ECDiffieHellman key)
    {
        var parameters = key.ExportParameters(false);
        return ComposePoint(parameters.Q.X!, parameters.Q.Y!);
    }

    /// <summary>Exports the 65-byte uncompressed point for a signing key (the VAPID public key).</summary>
    public static byte[] ExportUncompressedPoint(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        return ComposePoint(parameters.Q.X!, parameters.Q.Y!);
    }

    internal static ECDiffieHellman ImportPrivateKey(byte[] privateKey, byte[] publicKey) =>
        ECDiffieHellman.Create(ToParameters(privateKey, publicKey));

    internal static ECDsa ImportSigningKey(byte[] privateKey, byte[] publicKey) =>
        ECDsa.Create(ToParameters(privateKey, publicKey));

    private static ECParameters ToParameters(byte[] privateKey, byte[] publicKey)
    {
        if (publicKey.Length != UncompressedPointLength || publicKey[0] != 0x04)
            throw new ArgumentException("The public key must be a 65-byte uncompressed P-256 point.", nameof(publicKey));
        if (privateKey.Length != 32)
            throw new ArgumentException("The private key must be a 32-byte P-256 scalar.", nameof(privateKey));

        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = privateKey,
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] }
        };
    }

    private static ECDiffieHellman ImportPublicKey(byte[] publicKey) =>
        ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] }
        });

    private static byte[] ComposePoint(byte[] x, byte[] y)
    {
        var point = new byte[UncompressedPointLength];
        point[0] = 0x04;
        // A leading-zero coordinate exports shorter than 32 bytes; right-align it.
        x.CopyTo(point, 1 + (32 - x.Length));
        y.CopyTo(point, 33 + (32 - y.Length));
        return point;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
