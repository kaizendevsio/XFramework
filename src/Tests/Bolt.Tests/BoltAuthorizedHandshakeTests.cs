using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltAuthorizedHandshakeTests
{
    private const string TestScheme = "BoltHandshakeTest";
    private static int _portCounter = 20700;
    private WebApplication _app = null!;
    private TaskCompletionSource _stalledConnectionClosed = null!;
    private int _port;

    [SetUp]
    public async Task SetUp() => await StartServerAsync(BoltRegistrationIdentityBindingMode.Audit);

    [TearDown]
    public async Task TearDown()
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task BoltHandshake_QueryTokenOnProductionPath_Connects()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            "auth-client",
            "AuthClient",
            new BoltClientOptions
            {
                AccessToken = "valid-query-token",
                SendAccessTokenAsQueryString = true,
                RpcTimeoutSeconds = 5
            },
            NullLogger<BoltClient>.Instance);

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_ServiceScopeAndMatchingShaClientId_Connects()
    {
        const string serviceName = "XFramework.IdentityServer";
        await using var client = CreateClient(
            Sha256Hex(serviceName),
            serviceName,
            $"service:{serviceName}");

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_ServiceScpAndMatchingShaClientId_Connects()
    {
        const string serviceName = "XFramework.IdentityServer";
        await using var client = CreateClient(
            Sha256Hex(serviceName),
            serviceName,
            $"service-scp:{serviceName}");

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_AuditModeServiceIdentityMismatch_AllowsConnection()
    {
        const string tokenServiceName = "XFramework.IdentityServer";
        const string registeredServiceName = "XFramework.Portal";
        await using var client = CreateClient(
            Sha256Hex(registeredServiceName),
            registeredServiceName,
            $"service:{tokenServiceName}");

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_EnforceModeServiceIdentityMismatch_IsRejected()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        const string tokenServiceName = "XFramework.IdentityServer";
        const string registeredServiceName = "XFramework.Portal";
        await using var client = CreateClient(
            Sha256Hex(registeredServiceName),
            registeredServiceName,
            $"service:{tokenServiceName}");

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected registration*");
    }

    [Test]
    public async Task BoltHandshake_EnforceModeServiceClientWithWrongClientId_IsRejected()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        const string serviceName = "XFramework.IdentityServer";
        await using var client = CreateClient(
            "identity-server",
            serviceName,
            $"service:{serviceName}");

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected registration*");
    }

    [Test]
    public async Task BoltHandshake_EnforceModeUserClientWithNonReservedIdentity_Connects()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        await using var client = CreateClient(
            "browser-client",
            "BrowserClient",
            "user:portal-user");

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_EnforceModeUserClientWithDeterministicNonReservedIdentity_Connects()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        await using var client = CreateClient(
            Sha256Hex("BrowserClient"),
            "BrowserClient",
            "user:portal-user");

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task BoltHandshake_EnforceModeUserClientWithReservedServiceIdentity_IsRejected()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        const string serviceName = "XFramework.Portal";
        await using var client = CreateClient(
            Sha256Hex(serviceName),
            serviceName,
            "user:portal-user");

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected registration*");
    }

    [Test]
    public async Task BoltHandshake_EnforceModeUserClientWithReservedServiceClientId_IsRejected()
    {
        await RestartServerAsync(BoltRegistrationIdentityBindingMode.Enforce);

        await using var client = CreateClient(
            Sha256Hex(XFrameworkServiceNames.IdentityServer),
            "BrowserClient",
            "user:portal-user");

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected registration*");
    }

    [Test]
    public async Task BoltHandshake_MissingToken_IsRejected()
    {
        using var socket = new ClientWebSocket();

        var connect = async () => await socket.ConnectAsync(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            CancellationToken.None);

        await connect.Should().ThrowAsync<WebSocketException>();
    }

    [Test]
    public async Task BoltHandshake_QueryTokenOnNonBoltWsPath_IsRejected()
    {
        using var socket = new ClientWebSocket();

        var connect = async () => await socket.ConnectAsync(
            new Uri($"ws://localhost:{_port}/other/ws?access_token=valid-query-token"),
            CancellationToken.None);

        await connect.Should().ThrowAsync<WebSocketException>();
    }

    [Test]
    public async Task BoltHandshake_RegistrationAckNeverArrives_TimesOutAndDisposesTransport()
    {
        await using var client = new BoltClient(
            new Uri($"ws://localhost:{_port}/bolt/ws/stall"),
            "stall-client",
            "StallClient",
            new BoltClientOptions
            {
                AccessToken = "valid-query-token",
                SendAccessTokenAsQueryString = true,
                TransportAttemptTimeoutMs = 100
            },
            NullLogger<BoltClient>.Instance);

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*registration timed out after 100 ms*");
        await _stalledConnectionClosed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode)
                    return;
            }
            catch
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }

    private async Task RestartServerAsync(BoltRegistrationIdentityBindingMode bindingMode)
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        await StartServerAsync(bindingMode);
    }

    private async Task StartServerAsync(BoltRegistrationIdentityBindingMode bindingMode)
    {
        _port = Interlocked.Increment(ref _portCounter);
        _stalledConnectionClosed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddBoltServer(options =>
        {
            options.RegistrationIdentityBindingMode = bindingMode;
            options.ReservedServiceNames.AddRange(XFrameworkServiceNames.All);
            options.ReservedServiceNamePrefixes.Add("XFramework.");
        });
        builder.Services
            .AddAuthentication(TestScheme)
            .AddScheme<AuthenticationSchemeOptions, BoltHandshakeTestAuthHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws").RequireAuthorization();
        _app.MapBolt("/other/ws").RequireAuthorization();
        _app.Map("/bolt/ws/stall", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[256];
            try
            {
                await socket.ReceiveAsync(buffer, context.RequestAborted);
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            finally
            {
                _stalledConnectionClosed.TrySetResult();
            }
        }).RequireAuthorization();
        _app.MapGet("/health", () => Results.Ok("ok"));

        _ = Task.Run(() => _app.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
    }

    private BoltClient CreateClient(string clientId, string clientName, string accessToken) =>
        new(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            clientId,
            clientName,
            new BoltClientOptions
            {
                AccessToken = accessToken,
                SendAccessTokenAsQueryString = true,
                RpcTimeoutSeconds = 5
            },
            NullLogger<BoltClient>.Instance);

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class BoltHandshakeTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? token = null;

            if (Request.Path.StartsWithSegments("/bolt/ws") &&
                Request.Query.TryGetValue("access_token", out var queryToken))
            {
                token = queryToken.ToString();
            }

            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult(AuthenticateResult.NoResult());

            ClaimsIdentity identity;
            if (string.Equals(token, "valid-query-token", StringComparison.Ordinal))
            {
                identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "auth-client")], Scheme.Name);
            }
            else if (token.StartsWith("service:", StringComparison.Ordinal))
            {
                var serviceName = token["service:".Length..];
                identity = CreateServiceIdentity(serviceName, useScp: false);
            }
            else if (token.StartsWith("service-scp:", StringComparison.Ordinal))
            {
                var serviceName = token["service-scp:".Length..];
                identity = CreateServiceIdentity(serviceName, useScp: true);
            }
            else if (token.StartsWith("user:", StringComparison.Ordinal))
            {
                identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, token["user:".Length..])],
                    Scheme.Name);
            }
            else
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private ClaimsIdentity CreateServiceIdentity(string serviceName, bool useScp)
        {
            List<Claim> claims =
            [
                new("client_id", serviceName),
                new("service", serviceName),
                new(ClaimTypes.Name, serviceName)
            ];

            claims.Add(useScp
                ? new Claim("scp", $"profile {XFrameworkServiceScopes.BoltService}")
                : new Claim("scope", XFrameworkServiceScopes.BoltService));

            return new ClaimsIdentity(claims, Scheme.Name);
        }
    }
}
