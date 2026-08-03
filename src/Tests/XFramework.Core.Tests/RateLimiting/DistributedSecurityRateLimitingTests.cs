using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using XFramework.Core.RateLimiting;

namespace XFramework.Core.Tests.RateLimiting;

[TestFixture]
public sealed class DistributedSecurityRateLimitingTests
{
    [TestCase("POST", "/api/service-identity/token", "service-identity", 5, 60)]
    [TestCase("POST", "/api/service-identity/bolt-transport-token", "service-identity", 5, 60)]
    [TestCase("PATCH", "/api/verifications/34ed62ad-e830-4cf9-a428-29a03e7ef917/token", "verification", 5, 900)]
    public void PolicyMap_MapsStrictIdentityServerRoutes(
        string method,
        string path,
        string expectedName,
        int expectedLimit,
        int expectedWindowSeconds)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        StrictSecurityRateLimitPolicyMap.TryResolve(context.Request, out var policy).Should().BeTrue();
        policy.Name.Should().Be(expectedName);
        policy.PermitLimit.Should().Be(expectedLimit);
        policy.Window.Should().Be(TimeSpan.FromSeconds(expectedWindowSeconds));
    }

    [TestCase("GET", "/api/auth/authenticate")]
    [TestCase("POST", "/api/auth/authenticate")]
    [TestCase("POST", "/api/auth/logout")]
    [TestCase("POST", "/api/auth/forgot-password")]
    [TestCase("POST", "/api/auth/reset-password")]
    [TestCase("POST", "/api/auth/refresh")]
    [TestCase("POST", "/api/verifications/check")]
    public void PolicyMap_DoesNotMapOtherRoutes(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        StrictSecurityRateLimitPolicyMap.TryResolve(context.Request, out _).Should().BeFalse();
    }

    [Test]
    public void AuthenticationClientKey_NormalizesAddressAndIdentifierWithoutExposingEither()
    {
        var first = StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
            "::ffff:192.0.2.10",
            "  User@Example.com ");
        var equivalent = StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
            "192.0.2.10",
            "user@example.com");
        var differentIdentifier = StrictSecurityRateLimitPolicyMap.CreateAuthenticationClientKey(
            "192.0.2.10",
            "other@example.com");

        first.Should().Be(equivalent);
        first.Should().NotBe(differentIdentifier);
        first.Should().MatchRegex("^[0-9A-F]{64}$");
        first.Should().NotContain("192.0.2.10");
        first.Should().NotContain("USER");
    }

    [Test]
    public void TrustedProxyForwarding_TrustsOnlyLoopbackAndExplicitProxyAddresses()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TrustedProxyForwarding:KnownProxies:0"] = "192.0.2.10"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddXFrameworkTrustedProxyForwarding(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().Be(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(1);
        options.KnownIPNetworks.Should().BeEmpty();
        options.KnownProxies.Should().BeEquivalentTo(new[]
        {
            IPAddress.Loopback,
            IPAddress.IPv6Loopback,
            IPAddress.Parse("192.0.2.10")
        });
    }

    [Test]
    public async Task TrustedProxyForwarding_PartitionsProxiedClientsByForwardedAddress()
    {
        var limiter = new FakeLimiter(DistributedSecurityRateLimitDecision.Allowed);
        var pipeline = CreateForwardingPipeline(limiter, "192.0.2.10");
        var first = CreateContext("POST", "/api/service-identity/token");
        first.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        first.Request.Headers["X-Forwarded-For"] = "203.0.113.10";
        var second = CreateContext("POST", "/api/service-identity/token");
        second.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        second.Request.Headers["X-Forwarded-For"] = "203.0.113.11";

        await pipeline(first);
        await pipeline(second);

        first.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.10"));
        second.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.11"));
        limiter.ClientKeys.Should().HaveCount(2);
        limiter.ClientKeys[0].Should().NotBe(limiter.ClientKeys[1]);
    }

    [Test]
    public async Task TrustedProxyForwarding_IgnoresForwardedAddressFromUntrustedPeer()
    {
        var limiter = new FakeLimiter(DistributedSecurityRateLimitDecision.Allowed);
        var pipeline = CreateForwardingPipeline(limiter, "192.0.2.10");
        var context = CreateContext("POST", "/api/service-identity/token");
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50";

        await pipeline(context);

        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.20"));
        limiter.ClientKey.Should().Be(StrictSecurityRateLimitPolicyMap.CreateClientKey(context));
    }

    [Test]
    public void OptionsValidator_AllowsDisabledModeOnlyForDevelopmentAndTest()
    {
        var disabled = new DistributedSecurityRateLimitOptions { Enabled = false };

        new DistributedSecurityRateLimitOptionsValidator(Environment("Development"))
            .Validate(null, disabled).Succeeded.Should().BeTrue();
        new DistributedSecurityRateLimitOptionsValidator(Environment("Test"))
            .Validate(null, disabled).Succeeded.Should().BeTrue();
        new DistributedSecurityRateLimitOptionsValidator(Environment("Production"))
            .Validate(null, disabled).Failed.Should().BeTrue();
    }

    [Test]
    public void OptionsValidator_RequiresRedisWhenEnabled()
    {
        var result = new DistributedSecurityRateLimitOptionsValidator(Environment("Production"))
            .Validate(null, new DistributedSecurityRateLimitOptions { Enabled = true });

        result.Failed.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task HostStartup_FailsClosedForInvalidProductionConfiguration(bool enabled)
    {
        using var host = CreateHost("Production", enabled, null);

        Func<Task> start = () => host.StartAsync();

        await start.Should().ThrowAsync<OptionsValidationException>();
    }

    [Test]
    public async Task HostStartup_AllowsExplicitDevelopmentModeWithoutRedis()
    {
        using var host = CreateHost("Development", false, null);

        await host.StartAsync();
        await host.StopAsync();
    }

    [Test]
    public async Task HostStartup_FailsClosedWhenConfiguredRedisIsUnavailable()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "Unavailable"));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        using var host = CreateHost("Production", true, "redis:6379", redis.Object);

        Func<Task> start = () => host.StartAsync();

        await start.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The distributed security rate-limit store is unavailable.");
    }

    [Test]
    public async Task Middleware_AllowsPermittedRequestAndUsesOnlyRemoteAddress()
    {
        var limiter = new FakeLimiter(DistributedSecurityRateLimitDecision.Allowed);
        var nextCalled = false;
        var middleware = new DistributedSecurityRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("POST", "/api/service-identity/token");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50";

        await middleware.InvokeAsync(context, limiter);

        nextCalled.Should().BeTrue();
        limiter.ClientKey.Should().Be(StrictSecurityRateLimitPolicyMap.CreateClientKey(context));

        var sameRemoteAddress = CreateContext("POST", "/api/service-identity/token");
        sameRemoteAddress.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        sameRemoteAddress.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
        StrictSecurityRateLimitPolicyMap.CreateClientKey(sameRemoteAddress).Should().Be(limiter.ClientKey);
    }

    [Test]
    public async Task Middleware_ReturnsGeneric429WhenLimitIsExceeded()
    {
        var limiter = new FakeLimiter(
            DistributedSecurityRateLimitDecision.Rejected(TimeSpan.FromSeconds(12)));
        var nextCalled = false;
        var middleware = new DistributedSecurityRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("POST", "/api/service-identity/token");

        await middleware.InvokeAsync(context, limiter);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["Retry-After"].ToString().Should().Be("12");
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).Should().Contain("Too many requests.");
    }

    [Test]
    public async Task Middleware_FailsClosedWhenStoreThrows()
    {
        var middleware = new DistributedSecurityRateLimitMiddleware(_ =>
            throw new AssertionException("The endpoint must not execute."));
        var context = CreateContext("POST", "/api/service-identity/token");

        await middleware.InvokeAsync(context, new ThrowingLimiter());

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Test]
    public async Task Middleware_PropagatesRequestCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = CreateContext("POST", "/api/service-identity/token");
        context.RequestAborted = cancellation.Token;
        var middleware = new DistributedSecurityRateLimitMiddleware(_ => Task.CompletedTask);

        Func<Task> invoke = () => middleware.InvokeAsync(
            context,
            new FakeLimiter(DistributedSecurityRateLimitDecision.Allowed));

        await invoke.Should().ThrowAsync<OperationCanceledException>();
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static IHost CreateHost(
        string environmentName,
        bool enabled,
        string? connectionString,
        IConnectionMultiplexer? redis = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = environmentName
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DistributedSecurityRateLimitOptions.SectionName}:Enabled"] = enabled.ToString(),
            [$"{DistributedSecurityRateLimitOptions.SectionName}:RedisConnectionString"] = connectionString
        });
        if (redis is not null)
            builder.Services.AddSingleton(redis);
        builder.Services.AddDistributedStrictSecurityRateLimiting(builder.Configuration, builder.Environment);
        return builder.Build();
    }

    private static RequestDelegate CreateForwardingPipeline(
        IDistributedSecurityRateLimiter limiter,
        string trustedProxy)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TrustedProxyForwarding:KnownProxies:0"] = trustedProxy
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(limiter);
        services.AddXFrameworkTrustedProxyForwarding(configuration);
        var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);
        builder.UseXFrameworkTrustedProxyForwarding();
        builder.UseDistributedStrictSecurityRateLimiting();
        builder.Run(_ => Task.CompletedTask);
        return builder.Build();
    }

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }

    private sealed class FakeLimiter(DistributedSecurityRateLimitDecision decision)
        : IDistributedSecurityRateLimiter
    {
        public List<string> ClientKeys { get; } = [];
        public string? ClientKey { get; private set; }

        public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
            StrictSecurityRateLimitPolicy policy,
            string clientKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientKey = clientKey;
            ClientKeys.Add(clientKey);
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ThrowingLimiter : IDistributedSecurityRateLimiter
    {
        public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(
            StrictSecurityRateLimitPolicy policy,
            string clientKey,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<DistributedSecurityRateLimitDecision>(
                new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Unavailable"));
    }
}
