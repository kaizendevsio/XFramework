using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Notifications.Api.Services.Push;
using NUnit.Framework;

namespace Notifications.Tests.Services.Push;

public sealed class VapidSignerTests
{
    private const string VapidPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string VapidPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";

    private static readonly WebPushVapidKeys Keys = new(
        VapidPublic,
        PushBase64Url.Decode(VapidPublic),
        PushBase64Url.Decode(VapidPrivate),
        "mailto:ops@example.net");

    [Test]
    public void CreateToken_AudienceIsTheOriginWithoutThePath()
    {
        var token = VapidSigner.CreateToken(
            Keys,
            new Uri("https://fcm.googleapis.com/fcm/send/abc123?x=1"),
            DateTimeOffset.UnixEpoch);

        var claims = JsonDocument.Parse(PushBase64Url.Decode(token.Split('.')[1]));
        claims.RootElement.GetProperty("aud").GetString().Should().Be("https://fcm.googleapis.com");
        claims.RootElement.GetProperty("sub").GetString().Should().Be("mailto:ops@example.net");
        claims.RootElement.GetProperty("exp").GetInt64()
            .Should().Be((long)VapidSigner.DefaultLifetime.TotalSeconds);
    }

    [Test]
    public void CreateToken_UsesEs256WithAFixedWidthSignature()
    {
        var token = VapidSigner.CreateToken(Keys, new Uri("https://updates.push.services.mozilla.com/wpush/v2/x"), DateTimeOffset.UtcNow);
        var parts = token.Split('.');

        parts.Should().HaveCount(3);
        JsonDocument.Parse(PushBase64Url.Decode(parts[0])).RootElement.GetProperty("alg").GetString().Should().Be("ES256");
        // A DER signature would be 70-72 bytes and silently rejected by every push service.
        PushBase64Url.Decode(parts[2]).Should().HaveCount(64);
    }

    [Test]
    public void CreateToken_SignatureVerifiesAgainstThePublicKey()
    {
        var token = VapidSigner.CreateToken(Keys, new Uri("https://push.example.net/x"), DateTimeOffset.UtcNow);
        var parts = token.Split('.');

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Keys.PublicKeyBytes[1..33], Y = Keys.PublicKeyBytes[33..65] }
        });

        key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            PushBase64Url.Decode(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation).Should().BeTrue();
    }

    [Test]
    public void CreateAuthorizationHeader_CarriesTheTokenAndPublicKey()
    {
        var header = VapidSigner.CreateAuthorizationHeader(Keys, new Uri("https://push.example.net/x"), DateTimeOffset.UtcNow);

        header.Should().StartWith("vapid t=");
        header.Should().EndWith($", k={VapidPublic}");
    }
}
