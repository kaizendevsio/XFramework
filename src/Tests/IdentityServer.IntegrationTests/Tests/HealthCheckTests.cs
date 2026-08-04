using System.Diagnostics;
using System.Net;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
public class HealthCheckTests : IntegrationTestBase
{
    #region HTTP Tests

    [Test]
    public async Task Http_HealthCheck_ReturnsOkWithStatus()
    {
        var request = new HealthCheckRequest { Metadata = CreateMetadata() };

        var response = await HttpClient.PostAsJsonAsync("/api/health/check", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<HealthCheckResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Status.Should().Be("ok");
        result.Timestamp.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Http_HealthCheck_RespondsUnder250ms()
    {
        var request = new HealthCheckRequest { Metadata = CreateMetadata() };

        // Warmup
        await HttpClient.PostAsJsonAsync("/api/health/check", request);

        var sw = Stopwatch.StartNew();
        var response = await HttpClient.PostAsJsonAsync("/api/health/check",
            new HealthCheckRequest { Metadata = CreateMetadata() });
        sw.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sw.ElapsedMilliseconds.Should().BeLessThan(250,
            "health checks should remain bounded while including centralized invocation validation");

        TestContext.Out.WriteLine($"[HTTP] HealthCheck — {sw.Elapsed.TotalMilliseconds:F1}ms");
    }

    #endregion

    #region Bolt Tests

    [Test]
    public async Task Bolt_HealthCheck_ReturnsOkWithStatus()
    {
        var request = new HealthCheckRequest { Metadata = CreateMetadata() };

        var result = await IntegrationTestFixture.ServiceWrapper.HealthCheck(request);

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        result.Response.Should().NotBeNull();
        result.Response!.Status.Should().Be("ok");
        result.Response.Timestamp.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Bolt_HealthCheck_RespondsUnder250ms()
    {
        // Warmup
        await IntegrationTestFixture.ServiceWrapper.HealthCheck(
            new HealthCheckRequest { Metadata = CreateMetadata() });

        var sw = Stopwatch.StartNew();
        var result = await IntegrationTestFixture.ServiceWrapper.HealthCheck(
            new HealthCheckRequest { Metadata = CreateMetadata() });
        sw.Stop();

        result.Should().NotBeNull();
        result!.HttpStatusCode.Should().Be(HttpStatusCode.OK);
        sw.ElapsedMilliseconds.Should().BeLessThan(250,
            "health checks should remain bounded while including centralized invocation validation");

        TestContext.Out.WriteLine($"[Bolt] HealthCheck — {sw.Elapsed.TotalMilliseconds:F1}ms");
    }

    #endregion

    #region Helpers

    private static RequestMetadata CreateMetadata() => new()
    {
        RequestId = Guid.NewGuid(),
        IpAddress = "127.0.0.1",
        OperationName = "IntegrationTest",
        DeviceName = "TestDevice",
        UserAgent = "TestAgent"
    };

    #endregion
}
