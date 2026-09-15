using System.Net;
using System.Net.Http.Json;
using Communications.Domain.Shared.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

public sealed class PresenceEndpointTests
{
    [Test]
    public async Task Presence_RequiresSession_AndHonorsPerConversationPrivacy()
    {
        ChatFixture fixture = null!;
        var peer = Guid.NewGuid(); var hidden = false;
        await using var app = UiFixture.Create(0, configureChat: chat =>
        {
            fixture = chat;
            chat.Session.Setup(s => s.GetThreadAsync(chat.Thread, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ChatFixture.Ok(new GetThreadResponse { Id = chat.Thread, Name = "Presence test", IsDirect = true,
                    Members = [new() { CredentialId = chat.Credential }, new() { CredentialId = peer, HideActiveStatus = hidden }] }));
        });
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.That((await client.PostAsync("api/chat/presence", null)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        Assert.That((await client.PostAsync("api/chat/presence", null)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var presence = app.Services.GetRequiredService<YapPresence>();
        Assert.That(await presence.ActiveUntilAsync(fixture.Tenant, fixture.Credential, default), Is.Not.Null);
        await presence.TouchAsync(fixture.Tenant, peer, default);
        var conversation = (await client.GetFromJsonAsync<Conversation>($"api/chat/conversations/{fixture.Thread}"))!;
        Assert.That(conversation.People.Single(p => p.Id == peer).ActiveUntil, Is.Not.Null);
        hidden = true;
        conversation = (await client.GetFromJsonAsync<Conversation>($"api/chat/conversations/{fixture.Thread}"))!;
        Assert.That(conversation.People.Single(p => p.Id == peer).ActiveUntil, Is.Null);
        Assert.That(conversation.People.Single(p => p.Id == peer).LastActiveAt, Is.Null);
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        Assert.That((await client.PostAsync("api/chat/presence", null)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await app.StopAsync();
    }
}
