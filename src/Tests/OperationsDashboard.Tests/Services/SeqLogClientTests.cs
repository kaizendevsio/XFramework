using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Operations.Dashboard.Models;
using XFramework.Operations.Dashboard.Services;

namespace OperationsDashboard.Tests.Services;

[TestFixture]
public sealed class SeqLogClientTests
{
    [Test]
    public void BuildFilter_ApplicationAndMachineName_EscapesSeqStringLiterals()
    {
        var filter = SeqLogClient.BuildFilter("Identity'Server", "node-01");

        filter.Should().Be("Application = 'Identity''Server' and MachineName = 'node-01'");
    }

    [Test]
    public void BuildEventsUri_LogQuery_EncodesFilterAndClampsCount()
    {
        var uri = SeqLogClient.BuildEventsUri(new DashboardLogQuery(
            "Wallets",
            null,
            new DateTimeOffset(2026, 6, 19, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 19, 1, 30, 0, TimeSpan.Zero),
            1000));

        uri.Should().StartWith("api/events?count=500&render=true");
        uri.Should().Contain("filter=Application%20%3D%20%27Wallets%27");
    }

    [Test]
    public void ParseEvents_SeqResponse_ReturnsLogEvents()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "Events": [
                {
                  "Timestamp": "2026-06-19T01:00:00.0000000Z",
                  "Level": "Information",
                  "RenderedMessage": "Service started",
                  "Properties": {
                    "Application": "IdentityServer",
                    "MachineName": "node-01",
                    "SourceContext": "Identity.Program"
                  }
                }
              ]
            }
            """);

        var events = SeqLogClient.ParseEvents(document.RootElement);

        events.Should().ContainSingle();
        events[0].Application.Should().Be("IdentityServer");
        events[0].MachineName.Should().Be("node-01");
        events[0].Message.Should().Be("Service started");
    }

    [Test]
    public async Task GetLogsAsync_SeqNotConfigured_ReturnsUnavailable()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var client = new SeqLogClient(httpClient, NullLogger<SeqLogClient>.Instance);

        var result = await client.GetLogsAsync(new DashboardLogQuery(null, null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, 50));

        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Seq is not configured.");
    }

    [Test]
    public async Task GetLogsAsync_NonSuccessResponse_ReturnsUnavailable()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}"))
        {
            BaseAddress = new Uri("http://seq/")
        };
        var client = new SeqLogClient(httpClient, NullLogger<SeqLogClient>.Instance);

        var result = await client.GetLogsAsync(new DashboardLogQuery("IdentityServer", null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, 50));

        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Seq returned 503.");
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
