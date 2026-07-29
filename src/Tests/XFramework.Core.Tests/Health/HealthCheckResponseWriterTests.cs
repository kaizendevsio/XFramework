using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Text.Json;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NUnit.Framework;
using XFramework.Core.Health;

namespace XFramework.Core.Tests.Health;

[TestFixture]
public sealed class HealthCheckResponseWriterTests
{
    private static readonly HealthReport HealthyReport = new(
        new Dictionary<string, HealthReportEntry>(),
        TimeSpan.FromMilliseconds(1));

    [Test]
    public async Task PublicResponse_ContainsStatusOnly()
    {
        var context = CreateContext(IPAddress.Parse("100.64.0.10"));

        await HealthCheckResponseWriter.WriteResponse(context, HealthyReport);

        var response = await ReadResponse(context);
        response.Should().Contain("\"status\"").And.Contain("\"timestamp\"");
        response.Should().NotContain("\"checks\"").And.NotContain("\"data\"");
    }

    [Test]
    public async Task InternalResponse_NonLoopbackCaller_IsNotFoundWithoutBody()
    {
        var context = CreateContext(IPAddress.Parse("100.64.0.10"));

        await HealthCheckResponseWriter.WriteInternalResponse(context, HealthyReport);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await ReadResponse(context)).Should().BeEmpty();
    }

    [Test]
    public async Task InternalResponse_LoopbackCaller_ReceivesDetailedShape()
    {
        var context = CreateContext(IPAddress.Loopback);

        await HealthCheckResponseWriter.WriteInternalResponse(context, HealthyReport);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = await ReadResponse(context);
        response.Should().Contain("\"checks\"").And.Contain("\"duration\"");
    }

    [Test]
    public async Task InternalResponse_TransportSnapshots_MatchObservationSchemaExactly()
    {
        var clientSnapshot = new BoltClientHealthSnapshot(
            true, 1, 1, 0, 0, 0, 1, 1, 0, 0, 48, 30_000, 0, 0, 0, 0, 0);
        var serverSnapshot = new BoltServerHealthSnapshot(
            1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, false,
            new BoltServerHealthBounds(
                8 * 1024 * 1024, 4096, 30_000, 4 * 1024 * 1024, 2 * 1024 * 1024,
                10_000, 1_000, 8, 128, 32, 1_024, 1_024, 0, 0, 0, 0, false, true));
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Bolt-client-transport"] = CreateEntry(clientSnapshot),
                ["Bolt-transport"] = CreateEntry(serverSnapshot)
            },
            TimeSpan.FromMilliseconds(1));
        var context = CreateContext(IPAddress.Loopback);

        await HealthCheckResponseWriter.WriteInternalResponse(context, report);

        using var document = JsonDocument.Parse(await ReadResponse(context));
        PropertyNames(document.RootElement).Should().BeEquivalentTo(
            ["status", "duration", "timestamp", "checks"]);
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToDictionary(
            check => check.GetProperty("name").GetString()!,
            check => check);
        PropertyNames(checks["Bolt-client-transport"]).Should().BeEquivalentTo(
            ["name", "status", "description", "duration", "tags", "data", "exception"]);
        PropertyNames(checks["Bolt-client-transport"].GetProperty("data")).Should().Equal("transport");
        PropertyNames(checks["Bolt-client-transport"].GetProperty("data").GetProperty("transport"))
            .Should().BeEquivalentTo(
            [
                "isRegistered", "connectionCount", "connectedTransports", "pendingSends", "activeSends",
                "maxActiveSendElapsedMs", "runningSendLoops", "runningReceiveLoops", "faultedSendLoops",
                "faultedReceiveLoops", "pendingSendsUnhealthyThreshold", "activeSendUnhealthyThresholdMs",
                "totalSendFailures", "totalSendTimeouts", "totalReceiveLoopFaults",
                "totalUnexpectedDisconnects", "totalSuccessfulReconnects", "isHealthy"
            ]);
        PropertyNames(checks["Bolt-transport"].GetProperty("data")).Should().Equal("transport");
        var serverTransport = checks["Bolt-transport"].GetProperty("data").GetProperty("transport");
        PropertyNames(serverTransport).Should().BeEquivalentTo(
            [
                "acceptedConnections", "registeredConnections", "unregisteredConnections", "liveConnections",
                "closingConnections", "unregisteredTrackedConnections", "pendingRpcCalls", "activeLogicalStreams",
                "activeMediaStreams", "activeCalls", "activeSubscriptionReservations", "liveTransientSubscriptions",
                "liveDurableSubscriptions", "aggregateQueuedSendBytes", "maximumQueuedSendBytes",
                "connectionsUnderSendPressure", "runningSendLoops", "completedSendLoops", "faultedSendLoops",
                "liveConnectionsWithInactiveSendLoops", "negativeRuntimeCounters", "maximumConnectionsForOnePrincipal",
                "maximumPendingRpcCallsForOnePrincipal", "maximumLogicalStreamsForOnePrincipal",
                "maximumMediaStreamsForOnePrincipal", "maximumSubscriptionsForOnePrincipal", "isDisposed",
                "activeRateLimitPrincipals", "requestRateLimitRejections", "byteRateLimitRejections",
                "pushRateLimitRejections", "configuredBounds"
            ]);
        PropertyNames(serverTransport.GetProperty("configuredBounds")).Should().BeEquivalentTo(
            [
                "maximumFrameBytes", "sendQueueCapacityPerConnection", "sendEnqueueTimeoutMilliseconds",
                "sendBackpressureDropThresholdBytes", "sendBackpressureFeedbackThresholdBytes", "maximumPendingRpcCalls",
                "maximumPendingRpcCallsPerPrincipal", "maximumConnectionsPerPrincipal",
                "maximumLogicalStreamsPerPrincipal", "maximumMediaStreamsPerPrincipal",
                "maximumSubscriptionsPerPrincipal", "maximumDurableSubscribersPerTopic", "rpcRequestsPerSecond",
                "rpcRequestBurst", "rpcInboundBytesPerSecond", "rpcInboundByteBurst",
                "requireTopicAuthorization", "mediaEnabled"
            ]);
    }

    [TestCase("src/Presentation/XFramework.Portal/Program.cs")]
    [TestCase("src/Presentation/XFramework.Operations.Dashboard/Program.cs")]
    public void PresentationHost_UsesSharedHealthEndpointBoundary(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        source.Should().Contain("MapXFrameworkHealthChecks(");
        source.Should().NotContain("MapHealthChecks(");
        source.Should().NotContain("WriteHealthResponse");
    }

    private static DefaultHttpContext CreateContext(IPAddress remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteAddress;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponse(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static HealthReportEntry CreateEntry(object snapshot) => new(
        HealthStatus.Healthy,
        null,
        TimeSpan.Zero,
        null,
        new Dictionary<string, object> { ["transport"] = snapshot });

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).ToArray();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XFramework.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("XFramework repository root was not found.");
    }
}
