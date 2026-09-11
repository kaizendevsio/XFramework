using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class BrowserApiTests
{
    [Test]
    public async Task Events_ForSelectedConversation_StreamsTypingWithoutPolling()
    {
        await using var app = UiFixture.Create(0); await app.StartAsync();
        using var handler = new HttpClientHandler { CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        var account = $"{session.User!.TenantId:N}:{session.User.CredentialId:N}";
        client.DefaultRequestHeaders.Add("X-Yap-Account", account);
        var chat = (await client.GetFromJsonAsync<ChatPage<Conversation>>("api/chat/conversations"))!.Items[0];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.GetAsync($"api/chat/events?account={account}&thread={chat.Id}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/event-stream"));
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        Assert.That(await reader.ReadLineAsync(timeout.Token), Is.EqualTo(": connected"));
        await reader.ReadLineAsync(timeout.Token);
        await client.PostAsync("test/typing/true", null, timeout.Token);
        Assert.That(await reader.ReadLineAsync(timeout.Token), Is.EqualTo("event: typing"));
        var frame = (await reader.ReadLineAsync(timeout.Token))!;
        var update = System.Text.Json.JsonSerializer.Deserialize<TypingUpdate>(frame[6..])!;
        Assert.That(update.ThreadId, Is.EqualTo(chat.Id));
        Assert.That(update.IsTyping, Is.True);
        Assert.That(update.CredentialId, Is.Not.EqualTo(session.User.CredentialId));
        response.Dispose();
        await app.StopAsync();
    }

    [Test]
    public async Task ChatApi_RequiresAuthenticationAccountBindingAndAntiforgery()
    {
        await using var app = UiFixture.Create(0); await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        Assert.That((await client.GetAsync("api/chat/conversations")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var session = await client.GetFromJsonAsync<SessionResponse>("api/session");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session!.AntiforgeryToken);
        var login = await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await login.Content.ReadAsStringAsync(), Does.Contain("redirect"));
        session = await client.GetFromJsonAsync<SessionResponse>("api/session");
        Assert.That(session!.User, Is.Not.Null);
        Assert.That((await client.GetAsync("api/chat/conversations")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{Guid.NewGuid():N}");
        Assert.That((await client.GetAsync("api/chat/conversations")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        client.DefaultRequestHeaders.Remove("X-Yap-Account");
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User.TenantId:N}:{session.User.CredentialId:N}");
        var conversations = await client.GetAsync("api/chat/conversations");
        Assert.That(conversations.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(conversations.Headers.CacheControl?.NoStore, Is.True);
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        Assert.That((await client.PostAsJsonAsync("api/chat/initialize", new { })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        Assert.That((await client.PostAsJsonAsync("api/chat/initialize", new { })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await app.StopAsync();
    }
}
