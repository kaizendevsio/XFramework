using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

public sealed class ProfileCachingTests
{
    // Headers alone never proved anything: HttpClient has no cache, so a suite that only reads
    // Cache-Control passes just as happily against a response no browser would ever reuse. Every
    // assertion here is about what a second request actually costs.
    [Test]
    public async Task Avatar_IsImmutableAndPrivate_AndReplacementChangesTheAccountScopedUrl()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = await SignedInAsync(app);
        string? previous = null, previousTag = null;
        for (var i = 0; i < 2; i++)
        {
            var upload = await client.PostAsJsonAsync("api/chat/profile/photo", new YapProfile.ProfilePhoto([0xff, 0xd8, 0xff, (byte)i]));
            Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            var profile = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User!;
            Assert.That(profile.AvatarUrl, Does.Contain($"account={profile.TenantId:N}:{profile.CredentialId:N}"));
            Assert.That(profile.AvatarUrl, Is.Not.EqualTo(previous), "a replaced photo must be a different URL");
            var photo = await client.GetAsync(profile.AvatarUrl);
            Assert.That(photo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(photo.Headers.CacheControl?.Private, Is.True);
            Assert.That(photo.Headers.CacheControl?.Public, Is.False);
            // A year and immutable, not a day: the URL names one stored file forever, so there is
            // nothing a revalidation could ever discover, and immutable is what stops a reload or
            // a cold app start from asking again for a photo the device already holds.
            Assert.That(photo.Headers.CacheControl?.MaxAge, Is.EqualTo(TimeSpan.FromDays(365)));
            Assert.That(photo.Headers.CacheControl!.Extensions.Select(x => x.Name), Does.Contain("immutable"));
            // The version token in the URL is the validator, so the two can never disagree.
            var version = profile.AvatarUrl!.Split("&v=")[1];
            Assert.That(photo.Headers.ETag?.Tag, Is.EqualTo($"\"{version}\""));
            Assert.That(photo.Headers.ETag?.IsWeak, Is.False);
            Assert.That(photo.Headers.ETag!.Tag, Is.Not.EqualTo(previousTag), "a replaced photo must be a different validator");
            previous = profile.AvatarUrl;
            previousTag = photo.Headers.ETag.Tag;
        }
        await app.StopAsync();
    }

    // The regression itself: before this, a client that asked again - which is every cold start of
    // an installed app, whose HTTP cache the system has reclaimed - could only be answered with the
    // whole image again. There was no validator, so there was no cheap answer to give.
    [Test]
    public async Task Avatar_RevalidatesAsAnEmpty304_WithoutReadingStorageAgain()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = await SignedInAsync(app);
        await client.PostAsJsonAsync("api/chat/profile/photo", new YapProfile.ProfilePhoto([0xff, 0xd8, 0xff, 1]));
        var url = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User!.AvatarUrl!;
        var version = url.Split("&v=")[1];

        var first = await client.GetAsync(url);
        Assert.That((await first.Content.ReadAsByteArrayAsync()).Length, Is.GreaterThan(0));
        Assert.That(await UpstreamReadsAsync(client, version), Is.EqualTo(1));

        using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
        conditional.Headers.TryAddWithoutValidation("If-None-Match", first.Headers.ETag!.Tag);
        var revalidated = await client.SendAsync(conditional);
        Assert.That(revalidated.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        Assert.That(await revalidated.Content.ReadAsByteArrayAsync(), Is.Empty);
        Assert.That(revalidated.Headers.CacheControl?.Private, Is.True);
        Assert.That(await UpstreamReadsAsync(client, version), Is.EqualTo(1), "a revalidation must not re-read the file");

        // A stale validator is the one case that still has to cost a download, or a changed photo
        // would never arrive.
        using var stale = new HttpRequestMessage(HttpMethod.Get, url);
        stale.Headers.TryAddWithoutValidation("If-None-Match", $"\"{Guid.NewGuid():N}\"");
        var refreshed = await client.SendAsync(stale);
        Assert.That(refreshed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await refreshed.Content.ReadAsByteArrayAsync()).Length, Is.GreaterThan(0));
        Assert.That(await UpstreamReadsAsync(client, version), Is.EqualTo(2));
        await app.StopAsync();
    }

    [Test]
    public async Task GroupPhoto_CachesAndRevalidatesOnTheSameTermsAsAPerson()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = await SignedInAsync(app);
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        var conversations = (await client.GetFromJsonAsync<ChatPage<Conversation>>("api/chat/conversations?page=0"))!;
        var thread = conversations.Items.First(x => x.Group).Id;
        var upload = await client.PostAsJsonAsync($"api/chat/conversations/{thread}/photo", new YapProfile.ProfilePhoto([0xff, 0xd8, 0xff, 7]));
        Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var url = (await client.GetFromJsonAsync<ChatPage<Conversation>>("api/chat/conversations?page=0"))!
            .Items.Single(x => x.Id == thread).AvatarUrl!;
        Assert.That(url, Does.Contain($"account={session.User!.TenantId:N}:{session.User.CredentialId:N}"));

        var photo = await client.GetAsync(url);
        Assert.That(photo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(photo.Headers.CacheControl?.Private, Is.True);
        Assert.That(photo.Headers.CacheControl?.Public, Is.False);
        Assert.That(photo.Headers.CacheControl!.Extensions.Select(x => x.Name), Does.Contain("immutable"));
        Assert.That(photo.Headers.ETag?.Tag, Is.EqualTo($"\"{url.Split("&v=")[1]}\""));

        using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
        conditional.Headers.TryAddWithoutValidation("If-None-Match", photo.Headers.ETag!.Tag);
        var revalidated = await client.SendAsync(conditional);
        Assert.That(revalidated.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        Assert.That(await UpstreamReadsAsync(client, url.Split("&v=")[1]), Is.EqualTo(1));
        await app.StopAsync();
    }

    // The account query is what a native <img> carries instead of a header, and it is the only
    // thing separating one signed-in person's photo cache from another's on a shared device.
    [Test]
    public async Task Avatar_ForAnotherAccount_IsRefusedAndNeverCacheable()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = await SignedInAsync(app);
        await client.PostAsJsonAsync("api/chat/profile/photo", new YapProfile.ProfilePhoto([0xff, 0xd8, 0xff, 2]));
        var url = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User!.AvatarUrl!;
        var foreign = url[..url.IndexOf("account=", StringComparison.Ordinal)] + $"account={Guid.NewGuid():N}:{Guid.NewGuid():N}" + url[url.IndexOf("&v=", StringComparison.Ordinal)..];
        // An <img> cannot send the account header, so the query is the only claim being made.
        client.DefaultRequestHeaders.Remove("X-Yap-Account");
        using var request = new HttpRequestMessage(HttpMethod.Get, foreign);
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True, "a refusal must never be stored as the photo");
        await app.StopAsync();
    }

    private static async Task<int> UpstreamReadsAsync(HttpClient client, string version) =>
        (await client.GetFromJsonAsync<Dictionary<string, int>>("test/upstream-reads"))!.GetValueOrDefault(version);

    private static async Task<HttpClient> SignedInAsync(Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return client;
    }
}
