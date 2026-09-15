using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class SavedMessagesEndpointTests
{
    [Test]
    public async Task Saved_ListsBookmarkedMessagesNewestFirst_AndUnsaveRemovesTheRow()
    {
        await using var app = UiFixture.Create(0); await app.StartAsync();
        using var handler = new HttpClientHandler { CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var session = await SignInAsync(client);

        var saved = (await client.GetFromJsonAsync<ChatPage<SavedMessage>>("api/chat/saved"))!;
        Assert.That(saved.TotalCount, Is.EqualTo(2));
        Assert.Multiple(() =>
        {
            // The fixture saved the requester's own message most recently.
            Assert.That(saved.Items[0].Message.Sender, Is.EqualTo("You"));
            Assert.That(saved.Items[0].Message.Mine, Is.True);
            Assert.That(saved.Items[0].Message.Saved, Is.True);
            Assert.That(saved.Items[1].Message.Sender, Is.EqualTo("Sarah Mensah"));
            // A direct thread without a custom name is titled from the directory, like the inbox.
            Assert.That(saved.Items.Select(x => x.ConversationName), Is.All.EqualTo("Sarah Mensah"));
            Assert.That(saved.Items.Select(x => x.Group), Is.All.False);
            Assert.That(saved.Items.Select(x => x.Message.Text), Is.All.Not.Empty);
        });

        var removed = saved.Items[0].Message;
        var unsave = await client.PostAsJsonAsync("api/chat/message-actions",
            new MessageAction(removed.ThreadId, removed.Id, "unsave"));
        Assert.That(unsave.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var after = (await client.GetFromJsonAsync<ChatPage<SavedMessage>>("api/chat/saved"))!;
        Assert.That(after.TotalCount, Is.EqualTo(1));
        Assert.That(after.Items.Select(x => x.Message.Id), Does.Not.Contain(removed.Id));

        var resave = await client.PostAsJsonAsync("api/chat/message-actions",
            new MessageAction(removed.ThreadId, removed.Id, "save"));
        Assert.That(resave.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await client.GetFromJsonAsync<ChatPage<SavedMessage>>("api/chat/saved"))!.TotalCount, Is.EqualTo(2));
        Assert.That(session.User, Is.Not.Null);
        await app.StopAsync();
    }

    [Test]
    public async Task Saved_WithoutTheSignedInAccountBinding_IsRejected()
    {
        await using var app = UiFixture.Create(0); await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.That((await client.GetAsync("api/chat/saved")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        // Signed in, but the request claims a different account than the cookie holds.
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{Guid.NewGuid():N}");
        Assert.That((await client.GetAsync("api/chat/saved")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Remove("X-Yap-Account");
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User.TenantId:N}:{session.User.CredentialId:N}");
        var allowed = await client.GetAsync("api/chat/saved");
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(allowed.Headers.CacheControl?.NoStore, Is.True);
        await app.StopAsync();
    }

    private static async Task<SessionResponse> SignInAsync(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        // Signing in rotates the antiforgery token; posts must carry the one issued after login.
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return session;
    }
}
