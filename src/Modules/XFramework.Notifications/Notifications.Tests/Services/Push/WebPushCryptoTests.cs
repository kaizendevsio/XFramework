using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Notifications.Api.Services.Push;
using NUnit.Framework;

namespace Notifications.Tests.Services.Push;

/// <summary>
/// RFC 8291 section 5 "Push Message Encryption Example". A wrong derivation still produces a
/// well-formed body that a device silently fails to decrypt, so every intermediate value from the
/// RFC is asserted rather than only the final ciphertext.
/// </summary>
public sealed class WebPushCryptoTests
{
    // Published example values copied verbatim from RFC 8291 section 5. These are not credentials:
    // they exist so a wrong key derivation fails here instead of on someone's phone.
    private const string Plaintext = "When I grow up, I want to be a watermelon";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string ReceiverPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string ReceiverPrivate = "q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94";
    private const string SenderPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string SenderPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";

    private const string ExpectedBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml" +
        "mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT" +
        "pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Test]
    public void Encrypt_Rfc8291Section5Vector_ProducesTheDocumentedBody()
    {
        using var senderKey = WebPushCrypto.ImportPrivateKey(
            PushBase64Url.Decode(SenderPrivate),
            PushBase64Url.Decode(SenderPublic));

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            PushBase64Url.Decode(ReceiverPublic),
            PushBase64Url.Decode(AuthSecret),
            PushBase64Url.Decode(Salt),
            senderKey);

        PushBase64Url.Encode(body).Should().Be(ExpectedBody);
    }

    [Test]
    public void Encrypt_Rfc8291Section5Vector_DerivesTheDocumentedIntermediateValues()
    {
        using var senderKey = WebPushCrypto.ImportPrivateKey(
            PushBase64Url.Decode(SenderPrivate),
            PushBase64Url.Decode(SenderPublic));
        using var receiver = PublicKeyOf(ReceiverPublic);

        var ecdhSecret = senderKey.DeriveRawSecretAgreement(receiver.PublicKey);
        PushBase64Url.Encode(ecdhSecret).Should().Be("kyrL1jIIOHEzg3sM2ZWRHDRB62YACZhhSlknJ672kSs");

        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, PushBase64Url.Decode(AuthSecret));
        PushBase64Url.Encode(prkKey).Should().Be("Snr3JMxaHVDXHWJn5wdC52WjpCtd2EIEGBykDcZW32k");

        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, KeyInfo(ReceiverPublic, PushBase64Url.Decode(SenderPublic)));
        PushBase64Url.Encode(ikm).Should().Be("S4lYMb_L0FxCeq0WhDx813KgSYqU26kOyzWUdsXYyrg");

        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, PushBase64Url.Decode(Salt));
        PushBase64Url.Encode(prk).Should().Be("09_eUZGrsvxChDCGRCdkLiDXrReGOEVeSCdCcPBSJSc");

        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        PushBase64Url.Encode(cek).Should().Be("oIhVW04MRdy2XN9CiKLxTg");

        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        PushBase64Url.Encode(nonce).Should().Be("4h_95klXJ5E_qnoN");
    }

    [Test]
    public void Encrypt_WithRandomSenderKey_RoundTripsThroughTheReceiverKey()
    {
        var payload = Encoding.UTF8.GetBytes("{\"v\":1,\"kind\":\"message\"}");
        var body = WebPushCrypto.Encrypt(
            payload,
            PushBase64Url.Decode(ReceiverPublic),
            PushBase64Url.Decode(AuthSecret));

        Decrypt(body).Should().BeEquivalentTo(payload);
    }

    [Test]
    public void Encrypt_TwoCallsWithTheSameInput_UseFreshSaltAndSenderKey()
    {
        var first = WebPushCrypto.Encrypt("x"u8, PushBase64Url.Decode(ReceiverPublic), PushBase64Url.Decode(AuthSecret));
        var second = WebPushCrypto.Encrypt("x"u8, PushBase64Url.Decode(ReceiverPublic), PushBase64Url.Decode(AuthSecret));

        first[..16].Should().NotBeEquivalentTo(second[..16]);
        first[21..86].Should().NotBeEquivalentTo(second[21..86]);
    }

    [Test]
    public void Encrypt_HeaderLayout_MatchesRfc8188()
    {
        var body = WebPushCrypto.Encrypt(
            "x"u8,
            PushBase64Url.Decode(ReceiverPublic),
            PushBase64Url.Decode(AuthSecret));

        System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4))
            .Should().Be(WebPushCrypto.RecordSize);
        body[20].Should().Be(65, "the key id length byte precedes the uncompressed sender point");
        body[21].Should().Be(0x04, "the key id is an uncompressed P-256 point");
        body.Length.Should().Be(21 + 65 + 1 + 1 + 16, "header, one plaintext byte, the 0x02 delimiter and the GCM tag");
    }

    [Test]
    public void Encrypt_PlaintextLargerThanOneRecord_IsRejected() =>
        FluentActions.Invoking(() => WebPushCrypto.Encrypt(
                new byte[WebPushCrypto.MaxPlaintextBytes + 1],
                PushBase64Url.Decode(ReceiverPublic),
                PushBase64Url.Decode(AuthSecret)))
            .Should().Throw<ArgumentException>();

    [Test]
    public void Encrypt_MalformedReceiverKey_IsRejected() =>
        FluentActions.Invoking(() => WebPushCrypto.Encrypt(
                "x"u8,
                new byte[65],
                PushBase64Url.Decode(AuthSecret)))
            .Should().Throw<ArgumentException>();

    // An independent decryptor written straight from RFC 8188 section 2: it reads the header from
    // the wire instead of reusing the sender's layout assumptions.
    private static byte[] Decrypt(byte[] body)
    {
        var salt = body[..16];
        var idLength = body[20];
        var senderPublic = body[21..(21 + idLength)];
        var payload = body[(21 + idLength)..];

        using var receiverKey = WebPushCrypto.ImportPrivateKey(
            PushBase64Url.Decode(ReceiverPrivate),
            PushBase64Url.Decode(ReceiverPublic));
        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..65] }
        });

        var ecdhSecret = receiverKey.DeriveRawSecretAgreement(sender.PublicKey);
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, PushBase64Url.Decode(AuthSecret));
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, KeyInfo(ReceiverPublic, senderPublic));
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var ciphertext = payload[..^16];
        var tag = payload[^16..];
        var record = new byte[ciphertext.Length];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, ciphertext, tag, record);

        record[^1].Should().Be(0x02, "a single record ends with the last-record delimiter");
        return record[..^1];
    }

    private static byte[] KeyInfo(string receiverPublic, byte[] senderPublic) =>
        Encoding.ASCII.GetBytes("WebPush: info\0")
            .Concat(PushBase64Url.Decode(receiverPublic))
            .Concat(senderPublic)
            .ToArray();

    private static ECDiffieHellman PublicKeyOf(string publicKey)
    {
        var bytes = PushBase64Url.Decode(publicKey);
        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] }
        });
    }
}
