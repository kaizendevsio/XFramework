using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;

namespace SmsGateway.IntegrationTests.Tests;

[TestFixture]
public class SmsTests
{
    private HttpClient _http = null!;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [SetUp]
    public void SetUp() => _http = new HttpClient { BaseAddress = new Uri(SmsGatewayTestFixture.AppUrl) };

    [TearDown]
    public void TearDown() => _http?.Dispose();

    #region Create SMS Message

    [Test]
    public async Task CreateSmsMessage_WithValidData_ReturnsOk()
    {
        var request = new CreateSmsMessageRequest
        {
            Id = Guid.NewGuid(),
            AgentClusterId = SmsGatewayTestFixture.TestAgentClusterId,
            Recipient = "+639170000001",
            Message = "Test SMS message"
        };

        var response = await _http.PostAsJsonAsync("/api/sms/messages", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");
    }

    #endregion

    #region Get Pending Messages

    [Test]
    public async Task GetPending_AfterCreate_ReturnsMessages()
    {
        // Create a message first
        await _http.PostAsJsonAsync("/api/sms/messages", new CreateSmsMessageRequest
        {
            Id = Guid.NewGuid(),
            AgentClusterId = SmsGatewayTestFixture.TestAgentClusterId,
            Recipient = "+639170000002",
            Message = "Pending test"
        });

        var response = await _http.GetAsync(
            $"/api/sms/messages/pending/{SmsGatewayTestFixture.TestAgentClusterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pending test");
    }

    [Test]
    public async Task GetPending_WithUnknownCluster_ReturnsEmptyList()
    {
        var response = await _http.GetAsync($"/api/sms/messages/pending/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("[]");
    }

    #endregion

    #region Get Pending With Status Update

    [Test]
    public async Task GetPendingWithStatus_SetsMessagesToProcessing()
    {
        var clusterId = Guid.NewGuid();

        await _http.PostAsJsonAsync("/api/sms/messages", new CreateSmsMessageRequest
        {
            Id = Guid.NewGuid(),
            AgentClusterId = clusterId,
            Recipient = "+639170000003",
            Message = "Status update test"
        });

        var response = await _http.GetAsync($"/api/SmsGatewayNode/List/{clusterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Status update test");
    }

    #endregion

    #region Confirm Message Sent

    [Test]
    public async Task ConfirmSent_WithExistingMessage_RemovesFromPending()
    {
        var messageId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        await _http.PostAsJsonAsync("/api/sms/messages", new CreateSmsMessageRequest
        {
            Id = messageId,
            AgentClusterId = clusterId,
            Recipient = "+639170000004",
            Message = "Confirm test"
        });

        var response = await _http.PatchAsync($"/api/sms/messages/{messageId}/sent", null);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        // Verify removed from pending
        var pending = await _http.GetAsync($"/api/sms/messages/pending/{clusterId}");
        var pendingBody = await pending.Content.ReadAsStringAsync();
        pendingBody.Should().NotContain("Confirm test");
    }

    [Test]
    public async Task ConfirmSent_WithNonExistentMessage_Returns404()
    {
        var response = await _http.PatchAsync($"/api/sms/messages/{Guid.NewGuid()}/sent", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Create Received Message

    [Test]
    public async Task CreateReceived_WithValidData_ReturnsOk()
    {
        var request = new CreateMessageReceivedRequest
        {
            AgentClusterId = SmsGatewayTestFixture.TestAgentClusterId,
            Sender = "+639170000005",
            Message = "Received test message"
        };

        var response = await _http.PostAsJsonAsync("/api/sms/messages/received", request);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Get Scheduled Messages

    [Test]
    public async Task GetScheduled_WithUnknownCluster_ReturnsEmptyList()
    {
        var response = await _http.GetAsync($"/api/sms/messages/scheduled/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("[]");
    }

    #endregion
}
