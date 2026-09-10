using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class IdentityServerSigningKeyProviderTests
{
    [Test]
    public async Task IsAcceptedAsync_CanceledRefresh_AllowsNextCallerToRefresh()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new PolicyHandler((call, _) =>
        {
            if (call != 1) return Task.FromResult(AcceptedPolicy());
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var canceled = () => provider.IsAcceptedAsync("portal", "current", cancellation.Token);
        await canceled.Should().ThrowAsync<OperationCanceledException>();

        (await provider.IsAcceptedAsync("portal", "current")).Should().BeTrue();
        handler.Calls.Should().Be(2);
    }

    [Test]
    public async Task IsAcceptedAsync_FailedRefresh_DoesNotTurnOutageIntoCredentialRejection()
    {
        using var handler = new PolicyHandler((call, _) => Task.FromResult(call == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : AcceptedPolicy()));
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var failed = () => provider.IsAcceptedAsync("portal", "current");
        await failed.Should().ThrowAsync<InvalidOperationException>();

        (await provider.IsAcceptedAsync("portal", "current")).Should().BeTrue();
        handler.Calls.Should().Be(2);
    }

    [Test]
    public async Task IsAcceptedAsync_CompletedPolicy_RejectsUnknownGenerationWithoutRepeatedFetches()
    {
        using var handler = new PolicyHandler((_, _) => Task.FromResult(AcceptedPolicy()));
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        (await provider.IsAcceptedAsync("portal", "retired")).Should().BeFalse();
        (await provider.IsAcceptedAsync("portal", "retired")).Should().BeFalse();
        (await provider.IsAcceptedAsync("portal", "current")).Should().BeTrue();
        handler.Calls.Should().Be(1);
    }

    [Test]
    public async Task IsAcceptedAsync_RefreshingCallerCanceled_WaitingCallerRefreshesSuccessfully()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new PolicyHandler(async (call, ct) =>
        {
            if (call == 1)
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }
            return AcceptedPolicy();
        });
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var first = provider.IsAcceptedAsync("portal", "current", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiting = provider.IsAcceptedAsync("portal", "current");
        waiting.IsCompleted.Should().BeFalse();
        cancellation.Cancel();

        Func<Task> canceled = async () => await first;
        await canceled.Should().ThrowAsync<OperationCanceledException>();
        (await waiting.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        handler.Calls.Should().Be(2);
    }

    private static IdentityServerSigningKeyProvider CreateProvider(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(ServiceIdentityHttpClient.Name)).Returns(client);
        return new IdentityServerSigningKeyProvider(factory.Object, Options.Create(new ServiceIdentityOptions
        {
            Authority = "https://identity.example.test",
            CredentialGenerationCacheSeconds = 60
        }));
    }

    private static HttpResponseMessage AcceptedPolicy() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new ServiceSigningKeysResponse
        {
            CredentialGenerationsByClient = new() { ["portal"] = ["current"] }
        })
    };

    private sealed class PolicyHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => respond(++Calls, ct);
    }
}
