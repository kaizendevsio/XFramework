using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Yap.Contracts;
using Yap.Services;

namespace Yap.Tests;

public sealed class ProfileCachingTests
{
    [Test]
    public async Task Avatar_IsPrivatelyCached_AndReplacementChangesTheAccountScopedUrl()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new() }) { BaseAddress = new(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        string? previous = null;
        for (var i = 0; i < 2; i++)
        {
            var upload = await client.PostAsJsonAsync("api/chat/profile/photo", new YapProfile.ProfilePhoto([0xff, 0xd8, 0xff, (byte)i]));
            Assert.That(upload.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            var profile = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!.User!;
            Assert.That(profile.AvatarUrl, Does.Contain($"account={profile.TenantId:N}:{profile.CredentialId:N}"));
            Assert.That(profile.AvatarUrl, Is.Not.EqualTo(previous));
            var photo = await client.GetAsync(profile.AvatarUrl);
            Assert.That(photo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(photo.Headers.CacheControl?.Private, Is.True);
            Assert.That(photo.Headers.CacheControl?.Public, Is.False);
            Assert.That(photo.Headers.CacheControl?.MaxAge, Is.EqualTo(TimeSpan.FromDays(1)));
            previous = profile.AvatarUrl;
        }
        await app.StopAsync();
    }
}
