using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Yap.Services;

namespace Yap.Tests;

/// <summary>
/// Phase 3: where UDP call media gets its TURN servers. Cloudflare is mocked at the HTTP layer; the point is
/// what is asked, what each side gets, and that the API token goes nowhere but the Authorization header.
/// </summary>
[TestFixture]
public sealed class YapCallTransportTests
{
    private const string KeyId = "0123456789abcdef0123456789abcdef";
    private const string Token = "cf-api-token-never-leaves-the-server";
    private static readonly ClaimsPrincipal Participant = new(new ClaimsIdentity([new Claim("bolt_media_client_id", "yap-media-a-b")], "test"));

    private static readonly string CloudflareArray = """
        {"iceServers":[{"urls":["stun:stun.cloudflare.com:3478","stun:stun.cloudflare.com:53"]},
        {"urls":["turn:turn.cloudflare.com:3478?transport=udp","turn:turn.cloudflare.com:53?transport=udp","turn:turn.cloudflare.com:3478?transport=tcp",
        "turns:turn.cloudflare.com:5349?transport=tcp","turn:turn.cloudflare.com:80?transport=tcp","turns:turn.cloudflare.com:443?transport=tcp"],
        "username":"USER-N","credential":"CRED-N"}]}
        """;

    private sealed class Handler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (Requests) Requests.Add((request, body));
            return respond(request, Requests.Count);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingLogger : ILogger<YapTurnCredentials>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) =>
            Lines.Add(format(state, error) + error);
    }

    private static HttpResponseMessage Json(string body, int counter) => new(HttpStatusCode.Created)
    { Content = new StringContent(body.Replace("-N", "-" + counter), Encoding.UTF8, "application/json") };

    [Test]
    public async Task Cloudflare_IsAskedOncePerSide_WithTheBearerTokenAndTtl_AndEachSideGetsWhatItCanUse()
    {
        var handler = new Handler((_, n) => Json(CloudflareArray, n));
        var turn = new YapTurnOptions { CloudflareKeyId = KeyId, CloudflareApiToken = Token, CredentialTtlSeconds = 1800 };
        var credentials = new YapTurnCredentials(turn, new Factory(handler), NullLogger<YapTurnCredentials>.Instance);
        var before = DateTimeOffset.UtcNow;
        var grant = await credentials.GrantAsync(Participant, CancellationToken.None);

        Assert.That(grant, Is.Not.Null);
        Assert.That(handler.Requests, Has.Count.EqualTo(2), "the participant's credentials and the relay's own are minted separately");
        foreach (var (request, body) in handler.Requests)
        {
            Assert.Multiple(() =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.ToString(), Is.EqualTo($"https://rtc.live.cloudflare.com/v1/turn/keys/{KeyId}/credentials/generate-ice-servers"));
                Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
                Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo(Token));
                Assert.That(JsonDocument.Parse(body).RootElement.GetProperty("ttl").GetInt32(), Is.EqualTo(1800));
            });
        }
        var client = grant!.Client.Single();
        var relay = grant.Server.Single();
        Assert.Multiple(() =>
        {
            Assert.That(client.Credential, Is.EqualTo("CRED-1"));
            Assert.That(relay.Credential, Is.EqualTo("CRED-2"), "the relay's credential is not the one handed to the phone");
            Assert.That(client.Urls, Has.None.Match(@":53(\?|$)"), "browsers block port 53");
            Assert.That(client.Urls, Does.Contain("turns:turn.cloudflare.com:443?transport=tcp"), "TURN over TLS on 443 for networks that block UDP");
            Assert.That(client.Urls, Does.Contain("turn:turn.cloudflare.com:3478?transport=udp"));
            Assert.That(relay.Urls, Is.EquivalentTo(new[] { "turn:turn.cloudflare.com:3478?transport=udp", "turns:turn.cloudflare.com:443?transport=tcp" }),
                "the relay allocates over UDP, with TLS on 443 as its fallback");
            Assert.That(grant.Client.Concat(grant.Server).SelectMany(x => new[] { x.Username, x.Credential }.Concat(x.Urls)), Has.None.Contains(Token));
            Assert.That(grant.ExpiresAt, Is.EqualTo(before.AddSeconds(1800)).Within(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public void Cloudflare_SingleObjectAnswer_IsUnderstoodToo()
    {
        var servers = YapTurnCredentials.ParseIceServers(JsonDocument.Parse(
            """{"iceServers":{"urls":["turn:turn.cloudflare.com:3478?transport=udp"],"username":"u","credential":"c"}}""").RootElement);
        Assert.That(servers!.Single().Credential, Is.EqualTo("c"));
        Assert.That(YapTurnCredentials.ParseIceServers(JsonDocument.Parse("""{"iceServers":[{"urls":["stun:x:3478"]}]}""").RootElement), Is.Null,
            "STUN alone is no TURN grant");
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public async Task Cloudflare_Refusals_LeaveCallsOnTheWebSocket_AndNeverLogTheToken(HttpStatusCode status)
    {
        var logger = new CapturingLogger();
        var handler = new Handler((_, _) => new HttpResponseMessage(status) { Content = new StringContent("{\"errors\":[\"" + Token + "\"]}") });
        var credentials = new YapTurnCredentials(new YapTurnOptions { CloudflareKeyId = KeyId, CloudflareApiToken = Token }, new Factory(handler), logger);
        Assert.That(await credentials.GrantAsync(Participant, CancellationToken.None), Is.Null);
        Assert.That(logger.Lines, Has.Count.EqualTo(1));
        Assert.That(logger.Lines.Single(), Does.Contain(((int)status).ToString()).And.Not.Contain(Token));
    }

    [Test]
    public async Task Cloudflare_Unreachable_IsUnavailable_NotAnError()
    {
        var logger = new CapturingLogger();
        var handler = new Handler((_, _) => throw new HttpRequestException("connect to rtc.live.cloudflare.com failed " + Token));
        var credentials = new YapTurnCredentials(new YapTurnOptions { CloudflareKeyId = KeyId, CloudflareApiToken = Token }, new Factory(handler), logger);
        Assert.That(await credentials.GrantAsync(Participant, CancellationToken.None), Is.Null);
        Assert.That(string.Join("\n", logger.Lines), Does.Not.Contain(Token), "an exception message is never logged");
    }

    [Test]
    public async Task SharedSecret_DerivesShortLivedPerSessionCredentials_TheTurnRestWay()
    {
        var turn = new YapTurnOptions { Urls = ["turn:coturn:3478?transport=udp"], SharedSecret = "coturn-secret", CredentialTtlSeconds = 900 };
        var credentials = new YapTurnCredentials(turn, new Factory(new Handler((_, _) => throw new InvalidOperationException())), NullLogger<YapTurnCredentials>.Instance);
        var first = await credentials.GrantAsync(Participant, CancellationToken.None);
        var second = await credentials.GrantAsync(Participant, CancellationToken.None);
        var client = first!.Client.Single();
        var parts = client.Username!.Split(':');
        var expected = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes("coturn-secret"), Encoding.UTF8.GetBytes(client.Username)));
        Assert.Multiple(() =>
        {
            Assert.That(long.Parse(parts[0]), Is.EqualTo(DateTimeOffset.UtcNow.AddSeconds(900).ToUnixTimeSeconds()).Within(5), "the username carries its expiry");
            Assert.That(client.Credential, Is.EqualTo(expected));
            Assert.That(client.Username, Does.Not.Contain("yap-media"), "the label identifies a session, never a person");
            Assert.That(second!.Client.Single().Username, Is.Not.EqualTo(client.Username));
            Assert.That(first.Server.Single().Username, Does.EndWith(":relay"));
            Assert.That(first.Server.Single().Credential, Is.Not.EqualTo(client.Credential));
        });
    }

    [Test]
    public void Options_ComeFromConfiguration_WithTheTtlBounded()
    {
        var options = YapTurnOptions.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Yap:Calls:Turn:CloudflareKeyId"] = KeyId, ["Yap:Calls:Turn:CloudflareApiToken"] = Token,
            ["Yap:Calls:Turn:CredentialTtlSeconds"] = "60",
        }).Build());
        var generic = YapTurnOptions.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Yap:Calls:Turn:Urls"] = "turn:a:3478?transport=udp, turns:a:443?transport=tcp", ["Yap:Calls:Turn:Username"] = "u", ["Yap:Calls:Turn:Credential"] = "c",
        }).Build());
        Assert.Multiple(() =>
        {
            Assert.That(options.UsesCloudflare, Is.True);
            Assert.That(options.CredentialTtlSeconds, Is.EqualTo(600), "never shorter than a session needs to set up and renew");
            Assert.That(generic.UsesStatic, Is.True);
            Assert.That(generic.Urls, Has.Length.EqualTo(2));
            Assert.That(YapTurnOptions.From(new ConfigurationBuilder().Build()).IsConfigured, Is.False);
        });
    }

    [Test]
    public async Task WithoutTurn_OrWithoutTheSidecar_CallsStayOnWebSockets()
    {
        await using var none = Transport(new Dictionary<string, string?>());
        await using var noSidecar = Transport(new() { ["Yap:Calls:Turn:CloudflareKeyId"] = KeyId, ["Yap:Calls:Turn:CloudflareApiToken"] = Token,
            ["Yap:Calls:Rtc:SidecarPath"] = Path.Combine(Path.GetTempPath(), "missing-bolt-rtc") });
        Assert.Multiple(() =>
        {
            Assert.That(none.Options, Is.Null);
            Assert.That(none.Status, Does.StartWith("off"));
            Assert.That(noSidecar.Options, Is.Null);
            Assert.That(noSidecar.Status, Does.Contain("sidecar"));
        });
    }

    [Test]
    public async Task WithTurnAndTheSidecar_EncryptedGroupCallsOfferUdp()
    {
        var sidecar = Path.GetTempFileName();
        try
        {
            await using var transport = Transport(new() { ["Yap:Calls:Turn:CloudflareKeyId"] = KeyId, ["Yap:Calls:Turn:CloudflareApiToken"] = Token,
                ["Yap:Calls:Rtc:SidecarPath"] = sidecar });
            Assert.That(transport.Options, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(transport.Status, Is.EqualTo("on: Cloudflare TURN"));
                Assert.That(transport.Options!.RelayOnly, Is.True);
                Assert.That(transport.Status, Does.Not.Contain(Token));
            });
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:EncryptedGroups"] = "true", ["Yap:Calls:SecurityMode"] = "EndToEndEncrypted" }).Build();
            var scopes = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            using (var gateway = new YapCallGateway(configuration, scopes, NullLogger<Bolt.Server.BoltServer>.Instance, transport))
                Assert.That(gateway.Server.DatagramTransportEnabled, Is.True);
            var oneToOne = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Yap:Calls:Enabled"] = "true", ["Yap:Calls:SecurityMode"] = "TrustedServerTls" }).Build();
            using (var gateway = new YapCallGateway(oneToOne, scopes, NullLogger<Bolt.Server.BoltServer>.Instance, transport))
                Assert.That(gateway.Server.DatagramTransportEnabled, Is.False, "only end-to-end encrypted group calls take the datagram path");
        }
        finally { File.Delete(sidecar); }
    }

    [Test]
    public void Deployment_PassesTheTwoCloudflareValuesThrough_AndShipsTheSidecarWithYapOnly()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLAUDE.md"))) root = root.Parent;
        var compose = File.ReadAllText(Path.Combine(root!.FullName, "docker-compose.yml"));
        var yap = compose[compose.IndexOf("\n  yap:", StringComparison.Ordinal)..];
        yap = yap[..yap.IndexOf("\n  # ", 10, StringComparison.Ordinal)];
        var dockerfile = File.ReadAllText(Path.Combine(root.FullName, "Dockerfile"));
        Assert.Multiple(() =>
        {
            Assert.That(yap, Does.Contain("Yap__Calls__Turn__CloudflareKeyId: ${YAP_CALLS_TURN_CLOUDFLARE_KEY_ID:-}"));
            Assert.That(yap, Does.Contain("Yap__Calls__Turn__CloudflareApiToken: ${YAP_CALLS_TURN_CLOUDFLARE_API_TOKEN:-}"));
            Assert.That(dockerfile, Does.Contain("AS bolt-rtc"));
            Assert.That(dockerfile, Does.Match(@"\*XFramework\.Yap\.csproj\) \\\s+install -m 0755 /opt/bolt-rtc/bolt-rtc /app/publish/bolt-rtc"));
        });
    }

    private static YapCallTransport Transport(Dictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new Factory(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError))), NullLoggerFactory.Instance);
}
