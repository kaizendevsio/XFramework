using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

public sealed class ChatApiTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task GetAsync_TransientUnavailable_RetriesOnlyOnce(bool staysUnavailable)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => ++calls == 1 || staysUnavailable
            ? new(HttpStatusCode.ServiceUnavailable) : new(HttpStatusCode.OK) { Content = JsonContent.Create(42) })) { BaseAddress = new("https://yap.test/") };
        var api = new ChatApi(http);
        if (staysUnavailable) Assert.ThrowsAsync<ChatApiException>(() => api.GetAsync<int>("api/test"));
        else Assert.That(await api.GetAsync<int>("api/test"), Is.EqualTo(42));
        Assert.That(calls, Is.EqualTo(2));
    }

    [TestCase(401)]
    [TestCase(403)]
    [TestCase(429)]
    public void GetAsync_RejectedRead_DoesNotRetry(int status)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new((HttpStatusCode)status); })) { BaseAddress = new("https://yap.test/") };
        Assert.ThrowsAsync<ChatApiException>(() => new ChatApi(http).GetAsync<int>("api/test"));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void PostAsync_Unavailable_DoesNotReplayWrite()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.ServiceUnavailable); })) { BaseAddress = new("https://yap.test/") };
        Assert.ThrowsAsync<ChatApiException>(() => new ChatApi(http).PostAsync<int>("api/test", new { value = 1 }));
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GetAsync_AccountChangedOrCanceled_DoesNotRetry(bool canceled)
    {
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        ChatApi api = null!;
        using var http = new HttpClient(new Handler(_ =>
        {
            calls++;
            if (canceled) cancellation.Cancel(); else api.Account = "another-account";
            return new(HttpStatusCode.ServiceUnavailable);
        })) { BaseAddress = new("https://yap.test/") };
        api = new(http) { Account = "original-account" };
        Assert.That(async () => await api.GetAsync<int>("api/test", cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(calls, Is.EqualTo(1));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handle(request));
    }
}
