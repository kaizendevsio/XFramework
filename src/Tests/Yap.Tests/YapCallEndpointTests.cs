using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bolt.Protocol;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

[TestFixture]
public sealed class YapCallEndpointTests
{
    [TestCase(1)]
    [TestCase(2)]
    public async Task TwoAuthenticatedAccounts_ExchangeOpusPacketThroughHttpsGateway(int httpVersion)
    {
        using var certificate = CreateCertificate();
        await using var app = UiFixture.Create(0, enableCalls: true, certificate: certificate);
        await app.StartAsync();
        var origin = new UriBuilder(app.Urls.Single()) { Host = "localhost" }.Uri;
        using var caller = await LoginAsync(origin, "fixture", certificate);
        using var callee = await LoginAsync(origin, "callee", certificate);
        using var notificationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var account = $"{callee.User.TenantId:N}:{callee.User.CredentialId:N}";
        using var notifications = await callee.Http.GetAsync($"api/chat/events?account={account}", HttpCompletionOption.ResponseHeadersRead, notificationTimeout.Token);
        notifications.EnsureSuccessStatusCode();
        using var notificationReader = new StreamReader(await notifications.Content.ReadAsStreamAsync(notificationTimeout.Token));
        Assert.That(await notificationReader.ReadLineAsync(notificationTimeout.Token), Is.EqualTo(": connected"));
        await notificationReader.ReadLineAsync(notificationTimeout.Token);
        var chats = (await caller.Http.GetFromJsonAsync<ChatPage<Conversation>>("api/chat/conversations"))!;
        var invite = await PostAsync<YapCallInvite>(caller.Http, "api/chat/calls/", new StartYapCall(chats.Items[0].Id, callee.User.CredentialId));
        Assert.That(await notificationReader.ReadLineAsync(notificationTimeout.Token), Is.EqualTo("event: call"));
        var eventLine = await notificationReader.ReadLineAsync(notificationTimeout.Token);
        var incomingEvent = System.Text.Json.JsonSerializer.Deserialize<YapCallEvent>(eventLine![6..])!;
        Assert.That(incomingEvent.Type, Is.EqualTo("incoming"));
        Assert.That(incomingEvent.Invite.Id, Is.EqualTo(invite.Id), "The callee receives the invitation outside a selected conversation.");
        var outgoing = await PostAsync<YapCallConnection>(caller.Http, $"api/chat/calls/{invite.Id}/connect", new { });
        var incoming = await PostAsync<YapCallConnection>(callee.Http, $"api/chat/calls/{invite.Id}/connect", new { });
        using var a = await ConnectAsync(origin, outgoing, caller.SocketHttp, httpVersion);
        using var b = await ConnectAsync(origin, incoming, callee.SocketHttp, httpVersion);
        (await caller.Http.PostAsJsonAsync($"api/chat/calls/{invite.Id}/ready", new { })).EnsureSuccessStatusCode();
        (await callee.Http.PostAsJsonAsync($"api/chat/calls/{invite.Id}/ready", new { })).EnsureSuccessStatusCode();
        // A freshly subscribed caller must recover a ready event sent before its SSE connection existed.
        var callerAccount = $"{caller.User.TenantId:N}:{caller.User.CredentialId:N}";
        using var resumedEvents = await caller.Http.GetAsync($"api/chat/events?account={callerAccount}", HttpCompletionOption.ResponseHeadersRead, notificationTimeout.Token);
        using var resumedReader = new StreamReader(await resumedEvents.Content.ReadAsStreamAsync(notificationTimeout.Token));
        var sawRecipientReady = false;
        for (var lineIndex = 0; lineIndex < 9 && !sawRecipientReady; lineIndex++)
        {
            var line = await resumedReader.ReadLineAsync(notificationTimeout.Token);
            if (line?.StartsWith("data: ", StringComparison.Ordinal) != true) continue;
            var replay = System.Text.Json.JsonSerializer.Deserialize<YapCallEvent>(line[6..])!;
            sawRecipientReady = replay.Type == "ready" && replay.Invite.Id == invite.Id && replay.CredentialId == callee.User.CredentialId;
        }
        Assert.That(sawRecipientReady, Is.True, "SSE resubscription replays the connected recipient's ready state.");
        resumedEvents.Dispose();

        var target = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(target, BoltCodec.Fnv1aHash(outgoing.RecipientClientId));
        await SendAsync(a, writer => BoltCodec.WriteCallSignal(writer, invite.Id, SignalType.Initiate, target));
        var ringing = await ReceiveAsync(b, FrameType.CallSignal);
        Assert.That(BoltCodec.TryReadCallSignal(ringing, out var signal), Is.True);
        Assert.That(signal.SignalType, Is.EqualTo(SignalType.Initiate));
        await SendAsync(b, writer => BoltCodec.WriteCallSignal(writer, invite.Id, SignalType.Answer, []));
        await ReceiveSignalAsync(a, SignalType.Answer);

        var stream = Guid.NewGuid();
        await SendAsync(a, writer => BoltCodec.WriteMediaConfig(writer, stream, invite.Id, MediaType.Audio, CodecId.Opus, 48000, 1, 32, 0, []));
        var configuration = await ReceiveAsync(b, FrameType.MediaConfig);
        Assert.That(BoltCodec.TryReadMediaConfig(configuration, out var parsedConfig), Is.True);
        Assert.That(parsedConfig.CallId, Is.EqualTo(invite.Id));
        byte[] opus = [0xF8, 0xFF, 0xFE];
        await SendAsync(a, writer => BoltCodec.WriteMediaFrame(writer, stream, 1, 960, 0, opus));
        var packet = await ReceiveAsync(b, FrameType.MediaFrame);
        Assert.That(BoltCodec.TryReadMediaFrame(packet, out var parsedPacket), Is.True);
        Assert.That(parsedPacket.GetPayload(packet).ToArray(), Is.EqualTo(opus));

        await SendAsync(a, writer => BoltCodec.WriteCallSignal(writer, invite.Id, SignalType.End, []));
        await ReceiveSignalAsync(b, SignalType.End);
        a.Abort(); b.Abort();
        notifications.Dispose();
        notificationTimeout.Cancel();
        await app.StopAsync();
    }

    private static async Task<ClientWebSocket> ConnectAsync(Uri origin, YapCallConnection connection, HttpMessageInvoker invoker, int httpVersion)
    {
        var socket = new ClientWebSocket();
        socket.Options.HttpVersion = httpVersion == 2 ? HttpVersion.Version20 : HttpVersion.Version11;
        socket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        socket.Options.SetRequestHeader("Origin", origin.GetLeftPart(UriPartial.Authority));
        var url = new UriBuilder(new Uri(origin, connection.Url)) { Scheme = "wss" }.Uri;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(url, invoker, timeout.Token);
        await SendAsync(socket, writer => BoltCodec.WriteRegister(writer, connection.ClientId, "Yap voice fixture"));
        var registered = await ReceiveAsync(socket, FrameType.RegisterAck);
        Assert.That(registered[1], Is.EqualTo(1));
        return socket;
    }

    private static async Task SendAsync(ClientWebSocket socket, Action<IBufferWriter<byte>> write)
    {
        var writer = new ArrayBufferWriter<byte>(); write(writer);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.SendAsync(writer.WrittenMemory, WebSocketMessageType.Binary, true, timeout.Token);
    }

    private static async Task<byte[]> ReceiveAsync(ClientWebSocket socket, FrameType type)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[65536];
        while (true)
        {
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(length), timeout.Token);
                Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
                length += result.Count;
            } while (!result.EndOfMessage);
            if (buffer[0] == (byte)type) return buffer[..length];
        }
    }

    private static async Task ReceiveSignalAsync(ClientWebSocket socket, SignalType signal)
    {
        for (var i = 0; i < 8; i++)
        {
            var bytes = await ReceiveAsync(socket, FrameType.CallSignal);
            if (BoltCodec.TryReadCallSignal(bytes, out var parsed) && parsed.SignalType == signal) return;
        }
        Assert.Fail($"Expected call signal {signal}");
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object value)
    {
        using var response = await client.PostAsJsonAsync(url, value);
        Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<LoggedIn> LoginAsync(Uri origin, string name, X509Certificate2 certificate)
    {
        var cookies = new CookieContainer();
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies,
            ServerCertificateCustomValidationCallback = (_, received, _, errors) => MatchesCertificate(received, errors, certificate) }) { BaseAddress = origin };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        (await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["username"] = name, ["password"] = Environment.GetEnvironmentVariable("YAP_FIXTURE_PASSWORD") ?? "fixture" }))).EnsureSuccessStatusCode();
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        var socketHttp = new HttpMessageInvoker(new HttpClientHandler { CookieContainer = cookies,
            ServerCertificateCustomValidationCallback = (_, received, _, errors) => MatchesCertificate(received, errors, certificate) });
        return new(client, socketHttp, session.User);
    }

    private sealed record LoggedIn(HttpClient Http, HttpMessageInvoker SocketHttp, UserSession User) : IDisposable
    { public void Dispose() { Http.Dispose(); SocketHttp.Dispose(); } }

    // Exact certificate pin keeps the HTTPS fixture portable in CI without trusting arbitrary certificates.
    private static bool MatchesCertificate(X509Certificate? received, SslPolicyErrors errors, X509Certificate2 expected) =>
        received is not null && (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) == 0 &&
        received.GetRawCertData().AsSpan().SequenceEqual(expected.RawData);

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }
}
