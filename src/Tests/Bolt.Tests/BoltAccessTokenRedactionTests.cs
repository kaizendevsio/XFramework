using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Integration.Security;

namespace Bolt.Tests;

[TestFixture]
public sealed class BoltAccessTokenRedactionTests
{
    private const string Issuer = "bolt-redaction-tests";
    private const string Audience = "bolt-redaction-tests-client";

    [Test]
    public async Task InvokeAsync_BoltWebSocketRequest_RemovesTokenAndPreservesOtherQueryParameters()
    {
        var context = CreateContext(
            "/bolt/ws",
            "?access_token=secret-token&mode=full&tag=one&tag=two");
        var activity = new Activity("bolt-redaction-test").Start();
        activity.SetTag("url.query", "access_token=secret-token&mode=full&tag=one&tag=two");
        activity.SetTag("url.full", "https://localhost/bolt/ws?access_token=secret-token&mode=full&tag=one&tag=two");
        activity.SetTag("http.target", "/bolt/ws?access_token=secret-token&mode=full&tag=one&tag=two");
        activity.SetTag("http.url", "https://localhost/bolt/ws?access_token=secret-token&mode=full&tag=one&tag=two");

        string? observedQueryString = null;
        string? observedRawTarget = null;
        Dictionary<string, string?>? observedActivityTags = null;

        var middleware = new BoltAccessTokenRedactionMiddleware(nextContext =>
        {
            observedQueryString = nextContext.Request.QueryString.Value;
            observedRawTarget = nextContext.Features.Get<IHttpRequestFeature>()?.RawTarget;
            nextContext.Request.Query.ContainsKey("access_token").Should().BeFalse();
            nextContext.Request.Query["mode"].ToString().Should().Be("full");
            nextContext.Request.Query["tag"].ToArray().Should().Equal("one", "two");
            observedActivityTags = activity.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
            return Task.CompletedTask;
        });

        try
        {
            await middleware.InvokeAsync(context);
        }
        finally
        {
            activity.Stop();
        }

        observedQueryString.Should().Be("?mode=full&tag=one&tag=two");
        observedRawTarget.Should().Be("/bolt/ws?mode=full&tag=one&tag=two");
        observedActivityTags.Should().NotBeNull();
        observedActivityTags!.Values.Should().OnlyContain(value =>
            value == null || !value.Contains("secret-token", StringComparison.Ordinal));
        observedActivityTags["url.query"].Should().Be("mode=full&tag=one&tag=two");
        observedActivityTags["http.target"].Should().Be("/bolt/ws?mode=full&tag=one&tag=two");
    }

    [Test]
    public async Task InvokeAsync_NonBoltRequest_LeavesQueryAndTelemetryUnchanged()
    {
        const string query = "?access_token=secret-token&mode=full";
        var context = CreateContext("/other/ws", query);
        var activity = new Activity("non-bolt-redaction-test").Start();
        activity.SetTag("url.query", query[1..]);

        var middleware = new BoltAccessTokenRedactionMiddleware(nextContext =>
        {
            nextContext.Request.QueryString.Value.Should().Be(query);
            nextContext.Request.Query["access_token"].ToString().Should().Be("secret-token");
            activity.GetTagItem("url.query").Should().Be(query[1..]);
            return Task.CompletedTask;
        });

        try
        {
            await middleware.InvokeAsync(context);
        }
        finally
        {
            activity.Stop();
        }
    }

    [Test]
    public async Task JwtAuthentication_ValidBoltQueryToken_AuthenticatesAfterQueryRedaction()
    {
        var token = CreateToken();
        var result = await AuthenticateAsync("/bolt/ws", token);

        result.IsAuthenticated.Should().BeTrue();
        result.QueryString.Should().Be("?mode=full");
    }

    [Test]
    public async Task JwtAuthentication_TokenWithoutGenerationMetadata_IsRejected()
    {
        var token = CreateToken(includeGenerationMetadata: false);
        var result = await AuthenticateAsync("/bolt/ws", token);

        result.IsAuthenticated.Should().BeFalse();
        result.QueryString.Should().Be("?mode=full");
    }

    [Test]
    public async Task JwtAuthentication_InvalidBoltQueryToken_RemainsUnauthenticated()
    {
        var result = await AuthenticateAsync("/bolt/ws", "not-a-valid-jwt");

        result.IsAuthenticated.Should().BeFalse();
        result.QueryString.Should().Be("?mode=full");
    }

    [Test]
    public async Task JwtAuthentication_NonBoltQueryToken_IsNotConsumed()
    {
        var token = CreateToken();
        var result = await AuthenticateAsync("/other/ws", token);

        result.IsAuthenticated.Should().BeFalse();
        result.QueryString.Should().Contain("access_token=");
        result.QueryString.Should().Contain("mode=full");
    }

    private static DefaultHttpContext CreateContext(string path, string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        context.Features.Get<IHttpRequestFeature>()!.RawTarget = path + queryString;
        return context;
    }

    private static async Task<(bool IsAuthenticated, string QueryString)> AuthenticateAsync(
        string path,
        string token)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtOptions:GenerationId"] = "redaction-g0",
                ["JwtOptions:SigningPublicKeyPath"] = TestJwtKeyMaterial.PublicKeyPath,
                ["JwtOptions:ValidIssuer"] = Issuer,
                ["JwtOptions:ValidAudience"] = Audience
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.InstallJwt(configuration);

        await using var provider = services.BuildServiceProvider();
        var context = CreateContext(
            path,
            QueryString.Create(
                new Dictionary<string, string?>
                {
                    ["access_token"] = token,
                    ["mode"] = "full"
                }).Value!);
        context.RequestServices = provider;

        var isAuthenticated = false;
        string? observedQueryString = null;
        var application = new ApplicationBuilder(provider);
        application.UseMiddleware<BoltAccessTokenRedactionMiddleware>();
        application.UseAuthentication();
        application.Run(httpContext =>
        {
            isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
            observedQueryString = httpContext.Request.QueryString.Value;
            return Task.CompletedTask;
        });

        await application.Build()(context);

        return (isAuthenticated, observedQueryString!);
    }

    private static string CreateToken(bool includeGenerationMetadata = true)
    {
        var key = TestJwtKeyMaterial.CreateSigningKey("redaction-g0");
        if (includeGenerationMetadata)
            key.KeyId = "redaction-g0";
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.RsaSha512);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: includeGenerationMetadata
                ?
                [
                    new Claim(ClaimTypes.NameIdentifier, "bolt-client"),
                    new Claim(JwtCredentialSet.GenerationClaim, "redaction-g0")
                ]
                : [new Claim(ClaimTypes.NameIdentifier, "bolt-client")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
