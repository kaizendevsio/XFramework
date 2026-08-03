using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bolt.Client;
using Bolt.Hub.Configurations;
using Bolt.Hub.Installers;
using Bolt.Hub.Security;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(30000)]
public sealed class BoltTransportAuthenticationHandshakeTests
{
    private const string SigningKeyId = "bolt-rsa-g1";
    private const string ServiceName = "XFramework.IdentityServer";
    private const string MetadataAddress = "https://identity.test/.well-known/openid-configuration";
    private static int _portCounter = 21900;
    private RSA _signingRsa = null!;
    private RsaSecurityKey _signingKey = null!;
    private WebApplication _app = null!;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _signingRsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_signingRsa) { KeyId = SigningKeyId };
        await StartServerAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _app.StopAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        try { await _app.DisposeAsync(); } catch { }
        _signingRsa.Dispose();
    }

    [Test]
    public async Task BoltHandshake_ValidRsaTransportToken_Connects()
    {
        await using var client = CreateClient(
            ServiceName,
            CreateToken(new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)));

        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();
    }

    [TestCase(RejectedToken.Hmac)]
    [TestCase(RejectedToken.WrongSignature)]
    [TestCase(RejectedToken.WrongIssuer)]
    [TestCase(RejectedToken.WrongAudience)]
    [TestCase(RejectedToken.WrongType)]
    [TestCase(RejectedToken.MissingScope)]
    [TestCase(RejectedToken.Expired)]
    public async Task BoltHandshake_InvalidTransportToken_IsRejected(RejectedToken rejectedToken)
    {
        var token = CreateRejectedToken(rejectedToken);
        using var socket = new ClientWebSocket();
        var uri = new Uri(
            $"ws://localhost:{_port}/bolt/ws?access_token={Uri.EscapeDataString(token)}");

        var connect = async () => await socket.ConnectAsync(uri, CancellationToken.None);

        await connect.Should().ThrowAsync<WebSocketException>();
    }

    [Test]
    public async Task BoltHandshake_MismatchedRegisteredIdentity_IsRejected()
    {
        const string registeredServiceName = "XFramework.Portal";
        await using var client = CreateClient(
            registeredServiceName,
            CreateToken(new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)));

        var connect = async () => await client.ConnectAsync();

        await connect.Should().ThrowAsync<InvalidOperationException>();
        client.IsConnected.Should().BeFalse();
    }

    private async Task StartServerAsync()
    {
        _port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltTransportAuthentication:MetadataAddress"] = MetadataAddress,
            ["BoltTransportAuthentication:Issuer"] = BoltTransportAuthentication.ExpectedIssuer,
            ["BoltTransportAuthentication:Audience"] = BoltTransportAuthentication.ExpectedAudience,
            ["JwtOptions:GenerationId"] = "shared-rsa-g0",
            ["JwtOptions:SigningPublicKeyPath"] = TestJwtKeyMaterial.PublicKeyPath,
            ["JwtOptions:ValidIssuer"] = "shared-issuer",
            ["JwtOptions:ValidAudience"] = "shared-audience"
        });

        new BoltTransportAuthenticationInstaller()
            .InstallServices<BoltTransportAuthenticationHandshakeTests>(
                builder.Services,
                builder.Configuration,
                builder.Environment);
        builder.Services.InstallJwt(builder.Configuration);
        builder.Services.PostConfigure<JwtBearerOptions>(
            BoltTransportAuthentication.Scheme,
            options => options.ConfigurationManager = CreateConfigurationManager());
        builder.Services.AddBoltServer(options =>
        {
            options.RegistrationIdentityBindingMode = BoltRegistrationIdentityBindingMode.Enforce;
            options.ReservedServiceNames.AddRange(XFrameworkServiceNames.All);
            options.ReservedServiceNamePrefixes.Add("XFramework.");
        });
        builder.Services.AddAuthorization(BoltAuthorizationPolicies.AddTransportPolicy);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseMiddleware<BoltAccessTokenRedactionMiddleware>();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseWebSockets();
        _app.MapBolt("/bolt/ws")
            .RequireAuthorization(BoltAuthorizationPolicies.Transport);

        await _app.StartAsync();
    }

    private IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager()
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = BoltTransportAuthentication.ExpectedIssuer
        };
        configuration.SigningKeys.Add(_signingKey);
        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private BoltClient CreateClient(string registeredServiceName, string token) =>
        new(
            new Uri($"ws://localhost:{_port}/bolt/ws"),
            Sha256Hex(registeredServiceName),
            registeredServiceName,
            new BoltClientOptions
            {
                AccessToken = token,
                SendAccessTokenAsQueryString = true,
                RpcTimeoutSeconds = 5
            },
            NullLogger<BoltClient>.Instance);

    private string CreateRejectedToken(RejectedToken rejectedToken)
    {
        if (rejectedToken == RejectedToken.Hmac)
        {
            var hmacKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('h', 64)))
            {
                KeyId = "shared-hmac-g0"
            };
            return CreateToken(new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha512));
        }

        if (rejectedToken == RejectedToken.WrongSignature)
        {
            using var wrongRsa = RSA.Create(2048);
            var wrongKey = new RsaSecurityKey(wrongRsa) { KeyId = SigningKeyId };
            return CreateToken(new SigningCredentials(wrongKey, SecurityAlgorithms.RsaSha256));
        }

        return rejectedToken switch
        {
            RejectedToken.WrongIssuer => CreateToken(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
                issuer: "Wrong.Issuer"),
            RejectedToken.WrongAudience => CreateToken(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
                audience: "Wrong.Audience"),
            RejectedToken.WrongType => CreateToken(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
                tokenType: "JWT"),
            RejectedToken.MissingScope => CreateToken(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
                includeScope: false),
            RejectedToken.Expired => CreateToken(
                new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
                notBefore: DateTime.UtcNow.AddMinutes(-5),
                expires: DateTime.UtcNow.AddMinutes(-2)),
            _ => throw new ArgumentOutOfRangeException(nameof(rejectedToken), rejectedToken, null)
        };
    }

    private static string CreateToken(
        SigningCredentials signingCredentials,
        string issuer = BoltTransportAuthentication.ExpectedIssuer,
        string audience = BoltTransportAuthentication.ExpectedAudience,
        string tokenType = BoltTransportAuthentication.ExpectedTokenType,
        bool includeScope = true,
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        List<Claim> claims =
        [
            new("client_id", ServiceName),
            new("service", ServiceName),
            new("sub", ServiceName)
        ];
        if (includeScope)
            claims.Add(new Claim("scope", XFrameworkServiceScopes.BoltService));

        var header = new JwtHeader(signingCredentials)
        {
            [JwtHeaderParameterNames.Typ] = tokenType
        };
        var payload = new JwtPayload(
            issuer,
            audience,
            claims,
            notBefore ?? now.AddSeconds(-5),
            expires ?? now.AddMinutes(2),
            now);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public enum RejectedToken
    {
        Hmac,
        WrongSignature,
        WrongIssuer,
        WrongAudience,
        WrongType,
        MissingScope,
        Expired
    }
}
