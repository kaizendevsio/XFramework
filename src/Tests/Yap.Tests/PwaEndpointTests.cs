using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace Yap.Tests;

[TestFixture]
public sealed class PwaEndpointTests
{
    [Test]
    public async Task AnonymousInstall_HasPublicManifestAndCorrectlySizedIcons_WithoutBypassingLogin()
    {
        await using var app = UiFixture.Create(0);
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Urls.Single()) };
        foreach (var route in new[] { "/login", "/register" })
        {
            var html = await client.GetStringAsync(route);
            Assert.That(html, Does.Contain("rel=\"manifest\" href=\"manifest.webmanifest\""));
            Assert.That(html, Does.Contain("rel=\"apple-touch-icon\""));
        }

        var response = await client.GetAsync("/manifest.webmanifest");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/manifest+json"));
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;
        Assert.That(root.GetProperty("display").GetString(), Is.EqualTo("standalone"));
        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo("/"));
        Assert.That(root.GetProperty("scope").GetString(), Is.EqualTo("/"));
        var sizes = new List<string>();
        foreach (var icon in root.GetProperty("icons").EnumerateArray())
        {
            var size = icon.GetProperty("sizes").GetString()!;
            sizes.Add(size);
            await AssertIconAsync(client, icon.GetProperty("src").GetString()!, int.Parse(size.Split('x')[0]));
        }
        Assert.That(sizes, Is.EquivalentTo(new[] { "192x192", "512x512" }));
        await AssertIconAsync(client, "/apple-touch-icon.png", 180);
        var launch = await client.GetAsync(root.GetProperty("start_url").GetString());
        Assert.That(launch.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(new Uri(client.BaseAddress, launch.Headers.Location!).AbsolutePath, Is.EqualTo("/login"));
        await app.StopAsync();
    }

    private static async Task AssertIconAsync(HttpClient client, string path, int size)
    {
        var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"), path);
        var png = await response.Content.ReadAsByteArrayAsync();
        Assert.That(png.Length, Is.GreaterThan(24), path);
        Assert.That(png.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), path);
        Assert.That(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), Is.EqualTo(size), path);
        Assert.That(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)), Is.EqualTo(size), path);
    }
}
