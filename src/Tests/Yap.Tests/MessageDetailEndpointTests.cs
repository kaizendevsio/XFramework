using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Tests;

/// <summary>The two per-message detail routes. Names come from the authorized directory, not from a
/// message body, and a receipt is only ever the sender's to read.</summary>
public sealed class MessageDetailEndpointTests
{
    [Test]
    public async Task ReactionDetail_NamesEachReactorFromTheDirectory()
    {
        ChatFixture fixture = null!;
        await using var app = UiFixture.Create(0, configureChat: chat => fixture = chat);
        await app.StartAsync();
        using var client = await SignInAsync(app);
        var page = (await client.GetFromJsonAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{fixture.Thread}/messages?page=0"))!;
        var reacted = page.Items.Single(x => x.Reactions.Count > 0);
        Assert.That((await client.GetAsync($"api/chat/conversations/{fixture.Thread}/messages/{reacted.Id}/reactions")).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        var reactors = (await client.GetFromJsonAsync<List<MessageReactor>>(
            $"api/chat/conversations/{fixture.Thread}/messages/{reacted.Id}/reactions"))!;
        // Two people behind one badge is exactly what a count cannot say.
        Assert.That(reactors.Where(x => x.Emoji == "❤️").Select(x => x.Person.Name),
            Is.EquivalentTo(new[] { "Sarah Mensah", "Robin Chen" }));
        Assert.That(reactors.All(x => x.Person.Name != "Workspace member"), Is.True);
        Assert.That(reactors.Any(x => x.Mine), Is.False);
        await app.StopAsync();
    }

    [Test]
    public async Task ReceiptDetail_CarriesTimes_AndRefusesAnotherPersonsMessageWithoutFailing()
    {
        ChatFixture fixture = null!;
        await using var app = UiFixture.Create(0, configureChat: chat => fixture = chat);
        await app.StartAsync();
        using var client = await SignInAsync(app);
        var page = (await client.GetFromJsonAsync<ChatPage<ChatMessage>>($"api/chat/conversations/{fixture.Thread}/messages?page=0"))!;
        var mine = page.Items.Single(x => x.Mine && x.ReadCount > 0);
        var theirs = page.Items.First(x => !x.Mine);

        var own = (await client.GetFromJsonAsync<MessageReceiptDetail>(
            $"api/chat/conversations/{fixture.Thread}/messages/{mine.Id}/receipts"))!;
        Assert.That(own.ReadReceipts, Is.True);
        var entry = own.Entries.Single();
        Assert.That(entry.Person.Name, Is.EqualTo("Sarah Mensah"));
        Assert.That(entry.DeliveredAt, Is.GreaterThan(mine.CreatedAt));
        Assert.That(entry.ReadAt, Is.GreaterThan(entry.DeliveredAt));

        // Communications refuses someone else's message; that refusal is the answer, not an error.
        var response = await client.GetAsync($"api/chat/conversations/{fixture.Thread}/messages/{theirs.Id}/receipts");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var other = (await response.Content.ReadFromJsonAsync<MessageReceiptDetail>())!;
        Assert.That(other.Entries, Is.Empty);
        Assert.That(other.ReadReceipts, Is.False);
        await app.StopAsync();
    }

    [Test]
    public async Task MessageDetail_RequiresTheSignedInAccount()
    {
        ChatFixture fixture = null!;
        await using var app = UiFixture.Create(0, configureChat: chat => fixture = chat);
        await app.StartAsync();
        using var anonymous = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var path = $"api/chat/conversations/{fixture.Thread}/messages/{Guid.NewGuid()}";
        Assert.That((await anonymous.GetAsync($"{path}/reactions")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await anonymous.GetAsync($"{path}/receipts")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        await app.StopAsync();
    }

    private static async Task<HttpClient> SignInAsync(Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return client;
    }
}
