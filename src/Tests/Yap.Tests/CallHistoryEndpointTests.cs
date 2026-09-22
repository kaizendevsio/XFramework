using System.Net;
using System.Net.Http.Json;
using Communications.Domain.Shared.Contracts.Responses;
using Moq;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class CallHistoryEndpointTests
{
    [Test]
    public async Task Calls_AreAccountBoundAndReturnServerCallTypeAndConversationContext()
    {
        var id = Guid.NewGuid();
        await using var app = UiFixture.Create(0, configureChat: fixture =>
            fixture.Session.Setup(s => s.GetCallHistoryAsync(0, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatFixture.Ok(new SearchMessagesResponse { TotalCount = 1, Items = [new()
                { MessageId = id, ThreadId = fixture.Thread, SenderCredentialId = fixture.Credential, ThreadName = "Our group", Text = "Video call · 0:20", CreatedAt = DateTime.UtcNow }] })));
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() }) { BaseAddress = new(app.Urls.Single()) };
        Assert.That((await client.GetAsync("api/chat/calls/history")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{Guid.NewGuid():N}");
        Assert.That((await client.GetAsync("api/chat/calls/history")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Remove("X-Yap-Account");
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User.TenantId:N}:{session.User.CredentialId:N}");
        var response = await client.GetAsync("api/chat/calls/history");
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var page = (await response.Content.ReadFromJsonAsync<ChatPage<CallHistoryItem>>())!;
        Assert.That(page.Items.Single().Message.Id, Is.EqualTo(id));
        Assert.That(page.Items.Single().ConversationName, Is.EqualTo("Our group"));
        Assert.That(page.Items.Single().Group, Is.True);
        Assert.That(page.Items.Single().Message.Mine, Is.True);
        Assert.That(CallHistory.IsVideo(page.Items.Single().Message), Is.True);
        await app.StopAsync();
    }
}
