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

    [TestCase(null, false)]
    [TestCase("ended", true)]
    public void SendAsync_OnlyTheExplicitSignalMeansTheSessionEnded(string? signal, bool ended)
    {
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (signal is not null) response.Headers.Add(Yap.Contracts.SessionSignal.Header, signal);
            return response;
        })) { BaseAddress = new("https://yap.test/") };
        var error = Assert.ThrowsAsync<ChatApiException>(() => new ChatApi(http).GetAsync<int>("api/test"))!;
        Assert.That(error.SessionEnded, Is.EqualTo(ended));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SocketRequest_CarriesTheSessionEndedSignal(bool ended)
    {
        using var http = new HttpClient(new Handler(_ => throw new AssertionException("The socket answered"))) { BaseAddress = new("https://yap.test/") };
        var api = new ChatApi(http) { SocketRequest = (_, _, _) => Task.FromResult<Yap.Contracts.ChatSocketResponse?>(new(401, null, ended)) };
        var error = Assert.ThrowsAsync<ChatApiException>(() => api.PostAsync("api/chat/read", new { }))!;
        Assert.That(error.SessionEnded, Is.EqualTo(ended));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handle(request));
    }
}
