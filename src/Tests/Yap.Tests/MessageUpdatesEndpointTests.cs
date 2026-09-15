using System.Net;
using System.Net.Http.Json;
using Communications.Domain.Shared.Contracts.Responses;
using Moq;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class MessageUpdatesEndpointTests
{
    [Test]
    public async Task MessageUpdates_UseAuthenticatedTargetedQuery_AndBoundTheBatch()
    {
        ChatFixture fixture = null!;
        var id = Guid.NewGuid();
        await using var app = UiFixture.Create(0, configureChat: chat =>
        {
            fixture = chat;
            chat.Session.Setup(s => s.GetMessageUpdatesAsync(chat.Thread, It.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == id), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatFixture.Ok(new GetThreadMessagesResponse { Items = [new() { Id = id, Text = "targeted", SenderCredentialId = chat.Credential }], TotalCount = 1 }));
        });
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var path = $"api/chat/conversations/{fixture.Thread}/messages?ids={id}";
        Assert.That((await client.GetAsync(path)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        var page = (await client.GetFromJsonAsync<ChatPage<ChatMessage>>(path))!;
        Assert.That(page.Items.Single().Id, Is.EqualTo(id));
        Assert.That(page.Items.Single().Text, Is.EqualTo("targeted"));
        Assert.That((await client.GetAsync(path + string.Concat(Enumerable.Repeat($"&ids={id}", 50)))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        fixture.Session.Verify(s => s.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        await app.StopAsync();
    }
}
