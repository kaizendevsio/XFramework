using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Yap.Native;

public sealed record PairingInvitation(int Version, string Address, int Port, string CertificateSha256, string Secret)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this, NativeJson.Default.PairingInvitation));
    public static PairingInvitation Parse(string value)
    {
        if (value.Length > 2048) throw new ArgumentException("Invitation is too long.");
        var invite = JsonSerializer.Deserialize(Convert.FromBase64String(value.Trim()), NativeJson.Default.PairingInvitation)
            ?? throw new ArgumentException("Invalid invitation.");
        if (invite.Version != 1 || !IPAddress.TryParse(invite.Address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            invite.Port is < 1024 or > 65535 || Convert.FromHexString(invite.CertificateSha256).Length != 32 ||
            Convert.FromBase64String(invite.Secret).Length != 32)
            throw new ArgumentException("Unsupported invitation.");
        return invite;
    }
}

// Prototype-only LAN transport. No certificate-validation bypass: the exact public
// certificate is pinned by the invitation transferred outside this connection.
public sealed class PairingHost : IDisposable
{
    private readonly X509Certificate2 certificate;
    private readonly TcpListener listener;
    private readonly byte[] secret = RandomNumberGenerator.GetBytes(32);
    public PairingInvitation Invitation { get; }
    public PairingHost(string address)
    {
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Enter this phone's Wi-Fi IPv4 address.");
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Yap native benchmark", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(2));
        // Windows Schannel cannot use an ephemeral CNG key for TLS server auth.
        // This import creates a temporary user key container, released with the certificate.
        if (OperatingSystem.IsWindows())
        {
            var pfx = certificate.Export(X509ContentType.Pkcs12);
            try { certificate.Dispose(); certificate = X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.DefaultKeySet); }
            finally { CryptographicOperations.ZeroMemory(pfx); }
        }
        listener = new TcpListener(IPAddress.Any, 0); listener.Start(1);
        Invitation = new(1, address, ((IPEndPoint)listener.LocalEndpoint).Port,
            Convert.ToHexString(SHA256.HashData(certificate.RawData)), Convert.ToBase64String(secret));
    }
    public async Task<SslStream> AcceptAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var client = await listener.AcceptTcpClientAsync(timeout.Token);
        client.NoDelay = true; client.SendBufferSize = 65536; client.ReceiveBufferSize = 65536;
        listener.Stop(); // One peer, one run. Re-pair after failure or disconnect.
        var stream = new SslStream(client.GetStream(), false);
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(ct); handshake.CancelAfter(TimeSpan.FromSeconds(15));
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, handshake.Token);
            var offered = new byte[32]; await stream.ReadExactlyAsync(offered, handshake.Token);
            if (!CryptographicOperations.FixedTimeEquals(secret, offered)) throw new AuthenticationException("Pairing secret did not match.");
            await stream.WriteAsync(new byte[] { 1 }, handshake.Token);
            return stream;
        }
        catch { stream.Dispose(); client.Dispose(); throw; }
    }
    public void Dispose() { listener.Stop(); certificate.Dispose(); CryptographicOperations.ZeroMemory(secret); }
}

public static class PairingClient
{
    public static async Task<SslStream> ConnectAsync(PairingInvitation invite, CancellationToken ct)
    {
        var expected = Convert.FromHexString(invite.CertificateSha256);
        var client = new TcpClient { NoDelay = true, SendBufferSize = 65536, ReceiveBufferSize = 65536 };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        SslStream? stream = null;
        try
        {
            await client.ConnectAsync(IPAddress.Parse(invite.Address), invite.Port, timeout.Token);
            stream = new SslStream(client.GetStream(), false, (_, cert, _, _) => cert is not null &&
                CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(cert.GetRawCertData())));
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "Yap native benchmark", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeout.Token);
            await stream.WriteAsync(Convert.FromBase64String(invite.Secret), timeout.Token);
            var ack = new byte[1]; await stream.ReadExactlyAsync(ack, timeout.Token);
            if (ack[0] != 1) throw new AuthenticationException("Pairing rejected.");
            return stream;
        }
        catch { stream?.Dispose(); client.Dispose(); throw; }
    }
}
