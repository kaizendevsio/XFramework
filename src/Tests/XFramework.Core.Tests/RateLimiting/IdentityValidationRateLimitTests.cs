using System.Net;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Core.RateLimiting;

namespace XFramework.Core.Tests.RateLimiting;

[TestFixture]
public sealed class IdentityValidationRateLimitTests
{
    [Test]
    public void ValidationTraffic_DoesNotStarveSigningKeysOrGeneralTraffic()
    {
        using var services = CreateServices();
        using var limiter = services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var validation = Context("/api/auth/validate-session");
        // A shared hub can legitimately exceed the previous 100/minute global IP budget.
        for (var i = 0; i < 120; i++) AssertAllowed(limiter, validation);
        AssertAllowed(limiter, Context("/api/service-identity/signing-keys/query"));
        AssertAllowed(limiter, Context("/api/auth/authenticate"));
    }

    [TestCase("/api/auth/validate-session", 600)]
    [TestCase("/api/service-identity/signing-keys/query", 60)]
    [TestCase("/api/anything", 100)]
    public void RouteBudgets_RemainBoundedAndIsolatedByRemoteAddress(string path, int budget)
    {
        using var services = CreateServices();
        using var limiter = services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        var context = Context(path);
        for (var i = 0; i < budget; i++) AssertAllowed(limiter, context);
        using var rejected = limiter.AttemptAcquire(context);
        rejected.IsAcquired.Should().BeFalse();
        var anotherIp = Context(path);
        anotherIp.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
        AssertAllowed(limiter, anotherIp);
        // Headers and actor/client identifiers do not grant extra rate-limit partitions.
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.3";
        context.Request.Headers["X-Client-Id"] = "different";
        using var spoofed = limiter.AttemptAcquire(context);
        spoofed.IsAcquired.Should().BeFalse();
    }

    [Test]
    public void UnknownPathsAndWrongMethods_ShareTheOriginalGeneralBudget()
    {
        using var services = CreateServices();
        using var limiter = services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        for (var i = 0; i < 100; i++) AssertAllowed(limiter, Context($"/api/arbitrary/{i}"));
        var wrongMethod = Context("/api/auth/validate-session");
        wrongMethod.Request.Method = "GET";
        using var rejected = limiter.AttemptAcquire(wrongMethod);
        rejected.IsAcquired.Should().BeFalse();
        AssertAllowed(limiter, Context("/api/auth/validate-session"));
    }

    [Test]
    public void KeyQueryExhaustion_DoesNotBlockSessionChecks()
    {
        using var services = CreateServices();
        using var limiter = services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        for (var i = 0; i < 60; i++) AssertAllowed(limiter, Context("/api/service-identity/signing-keys/query"));
        using var rejected = limiter.AttemptAcquire(Context("/api/service-identity/signing-keys/query"));
        rejected.IsAcquired.Should().BeFalse();
        AssertAllowed(limiter, Context("/api/auth/validate-session"));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXFrameworkRateLimiting();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        return context;
    }

    private static void AssertAllowed(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context);
        lease.IsAcquired.Should().BeTrue();
    }
}
