using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;
using Storage.Domain.Shared.Contracts.Responses;
using Yap.Contracts;

namespace Yap.Tests;

public sealed class VideoStreamingTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task NativeVideo_RequiresAccountBinding_AndPreservesPartialAndUnsatisfiableRanges(bool unsatisfiable)
    {
        var upstreamCalls = 0;
        using var upstream = new HttpClient(new RangeHandler(request =>
        {
            upstreamCalls++;
            Assert.That(request.Headers.Range!.ToString(), Is.EqualTo("bytes=10-19"));
            var response = new HttpResponseMessage(unsatisfiable ? HttpStatusCode.RequestedRangeNotSatisfiable : HttpStatusCode.PartialContent)
            { Content = new ByteArrayContent(unsatisfiable ? [] : new byte[10]) };
            response.Content.Headers.ContentType = new("application/octet-stream");
            response.Content.Headers.ContentRange = unsatisfiable ? new ContentRangeHeaderValue(5) : new ContentRangeHeaderValue(10, 19, 100);
            return response;
        }));
        await using var app = UiFixture.Create(0, configureChat: fixture =>
            fixture.Session.Setup(x => x.GetAttachmentDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChatFixture.Ok(new StorageDownloadUrlResponse { Url = "https://storage.test/video" })),
            configureServices: services => services.AddSingleton(Mock.Of<IHttpClientFactory>(x => x.CreateClient("attachments") == upstream)));
        await app.StartAsync();
        using var handler = new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new(app.Urls.Single()) };
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "fixture", ["password"] = "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        var route = $"api/chat/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}";
        client.DefaultRequestHeaders.Range = new(10, 19);
        Assert.That((await client.GetAsync(route)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await client.GetAsync(route + "?account=other")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(upstreamCalls, Is.Zero);
        var account = $"{session.User!.TenantId:N}:{session.User.CredentialId:N}";
        using var result = await client.GetAsync(route + $"?account={account}&mediaType=video/quicktime");
        Assert.That(result.StatusCode, Is.EqualTo(unsatisfiable ? HttpStatusCode.RequestedRangeNotSatisfiable : HttpStatusCode.PartialContent));
        Assert.That(result.Content.Headers.ContentRange!.ToString(), Is.EqualTo(unsatisfiable ? "bytes */5" : "bytes 10-19/100"));
        Assert.That((await result.Content.ReadAsByteArrayAsync()).Length, Is.EqualTo(unsatisfiable ? 0 : 10));
        Assert.That(result.Content.Headers.ContentType!.MediaType, Is.EqualTo("video/quicktime"));
        Assert.That(result.Headers.CacheControl!.NoStore, Is.True);
    }

    private sealed class RangeHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handle(request));
    }
}
