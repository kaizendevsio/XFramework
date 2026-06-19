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
public sealed class JaegerTraceClientTests
{
    [Test]
    public void BuildTracesUri_Query_EncodesServiceAndClampsLimit()
    {
        var uri = JaegerTraceClient.BuildTracesUri(new DashboardTraceQuery("Identity Server", TimeSpan.FromMinutes(30), 500));

        uri.Should().Be("api/traces?service=Identity%20Server&lookback=1h&limit=100");
    }

    [Test]
    public void ParseTraces_JaegerResponse_ReturnsTimelineSpans()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "data": [
                {
                  "traceID": "abc",
                  "spans": [
                    {
                      "spanID": "root",
                      "operationName": "GET /api/users",
                      "processID": "p1",
                      "startTime": 1781830800000000,
                      "duration": 100000,
                      "tags": []
                    },
                    {
                      "spanID": "child",
                      "operationName": "SELECT users",
                      "processID": "p1",
                      "startTime": 1781830800050000,
                      "duration": 25000,
                      "tags": [
                        { "key": "otel.status_code", "value": "ERROR" }
                      ]
                    }
                  ],
                  "processes": {
                    "p1": {
                      "serviceName": "IdentityServer"
                    }
                  }
                }
              ]
            }
            """);

        var traces = JaegerTraceClient.ParseTraces(document.RootElement);

        traces.Should().ContainSingle();
        traces[0].TraceId.Should().Be("abc");
        traces[0].RootOperation.Should().Be("GET /api/users");
        traces[0].Duration.Should().Be(TimeSpan.FromMilliseconds(100));
        traces[0].HasErrors.Should().BeTrue();
        traces[0].Spans[1].Offset.Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [Test]
    public async Task GetTracesAsync_JaegerNotConfigured_ReturnsUnavailable()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var client = new JaegerTraceClient(httpClient, NullLogger<JaegerTraceClient>.Instance);

        var result = await client.GetTracesAsync(new DashboardTraceQuery("IdentityServer", TimeSpan.FromMinutes(30), 20));

        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Jaeger is not configured.");
    }

    [Test]
    public async Task GetTracesAsync_NonSuccessResponse_ReturnsUnavailable()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}"))
        {
            BaseAddress = new Uri("http://jaeger/")
        };
        var client = new JaegerTraceClient(httpClient, NullLogger<JaegerTraceClient>.Instance);

        var result = await client.GetTracesAsync(new DashboardTraceQuery("IdentityServer", TimeSpan.FromMinutes(30), 20));

        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Jaeger returned 503.");
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
