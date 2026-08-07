using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bolt.Client;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Bolt.Tests;

[TestFixture]
[CancelAfter(15000)]
public sealed class IdentityServerTokenProviderTests
{
    private const string ClientId = "XFramework.TestClient";
    private const string ClientSecret = "test-client-secret-material-at-least-32-bytes";

    [Test]
    public async Task BoltTransportProvider_ValidToken_CachesUntilRefreshWindow()
    {
        var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var requestCount = 0;
        using var factory = CreateFactory((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(CreateTokenResponse("transport-token", now.AddMinutes(2)));
        });
        var options = CreateOptions(refreshSkewSeconds: 60);
        var provider = CreateTransportProvider(factory, options, timeProvider);

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(61));
        var refreshed = await provider.GetTokenAsync();

        first.Should().Be("transport-token");
        second.Should().Be("transport-token");
        refreshed.Should().Be("transport-token");
        requestCount.Should().Be(2);
    }

    [Test]
    public async Task BoltTransportProvider_ConcurrentCallers_UsesSingleHttpRequest()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        using var factory = CreateFactory(async (_, ct) =>
        {
            Interlocked.Increment(ref requestCount);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return CreateTokenResponse("shared-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateTransportProvider(factory, CreateOptions());

        var calls = Enumerable.Range(0, 20)
            .Select(_ => provider.GetTokenAsync().AsTask())
            .ToArray();
        await entered.Task;
        requestCount.Should().Be(1);
        release.TrySetResult();

        var tokens = await Task.WhenAll(calls);

        tokens.Should().OnlyContain(token => token == "shared-token");
        requestCount.Should().Be(1);
    }

    [Test]
    public async Task BoltTransportProvider_CallerCancels_PreservesSharedAcquisition()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        using var factory = CreateFactory(async (_, ct) =>
        {
            Interlocked.Increment(ref requestCount);
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return CreateTokenResponse("shared-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateTransportProvider(factory, CreateOptions());
        using var callerCts = new CancellationTokenSource();

        var canceledCall = provider.GetTokenAsync(callerCts.Token).AsTask();
        await entered.Task;
        callerCts.Cancel();
        var sharedCall = provider.GetTokenAsync().AsTask();

        var awaitCanceledCall = async () => await canceledCall;
        await awaitCanceledCall.Should().ThrowAsync<OperationCanceledException>();
        release.TrySetResult();
        (await sharedCall).Should().Be("shared-token");
        requestCount.Should().Be(1);
    }

    [Test]
    public async Task BoltTransportProvider_HttpFailure_ClearsSingleFlightForRetry()
    {
        var requestCount = 0;
        using var factory = CreateFactory((_, _) =>
        {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : CreateTokenResponse("retry-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        });
        var provider = CreateTransportProvider(factory, CreateOptions());

        var firstCall = async () => await provider.GetTokenAsync();
        await firstCall.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*HTTP status 401*");

        var retryStopwatch = Stopwatch.StartNew();
        (await provider.GetTokenAsync()).Should().Be("retry-token");
        retryStopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400));
        retryStopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        requestCount.Should().Be(2);
    }

    [Test]
    public async Task BoltTransportProvider_RequestExceedsDeadline_ThrowsTimeoutAndAllowsRetry()
    {
        var requestCount = 0;
        using var factory = CreateFactory(async (_, ct) =>
        {
            var attempt = Interlocked.Increment(ref requestCount);
            if (attempt == 1)
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);

            return CreateTokenResponse("retry-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateTransportProvider(
            factory,
            CreateOptions(tokenAcquisitionTimeoutSeconds: 1));
        var stopwatch = Stopwatch.StartNew();

        var timedOutCall = async () => await provider.GetTokenAsync();
        await timedOutCall.Should().ThrowAsync<TimeoutException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));

        (await provider.GetTokenAsync()).Should().Be("retry-token");
        requestCount.Should().Be(2);
    }

    [Test]
    public async Task BoltTransportProvider_Request_UsesIdentityServerHttpContract()
    {
        HttpMethod? method = null;
        Uri? requestUri = null;
        string? authorization = null;
        JsonElement body = default;
        using var factory = CreateFactory(async (request, ct) =>
        {
            method = request.Method;
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            return CreateTokenResponse("transport-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateTransportProvider(factory, CreateOptions());

        await provider.GetTokenAsync();

        factory.RequestedClientNames.Should().Equal(ServiceIdentityHttpClient.Name);
        method.Should().Be(HttpMethod.Post);
        requestUri.Should().Be("https://identity.test/api/service-identity/bolt-transport-token");
        authorization.Should().BeNull();
        body.GetProperty("clientId").GetString().Should().Be(ClientId);
        body.GetProperty("clientSecret").GetString().Should().Be(ClientSecret);
    }

    [Test]
    public async Task ServiceTokenProvider_Request_UsesHttpAndCachesByNormalizedAudienceAndScopes()
    {
        var requestCount = 0;
        Uri? requestUri = null;
        JsonElement body = default;
        using var factory = CreateFactory(async (request, ct) =>
        {
            Interlocked.Increment(ref requestCount);
            requestUri = request.RequestUri;
            body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            return CreateTokenResponse("service-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateServiceProvider(factory, CreateOptions());

        var first = await provider.GetTokenAsync(
            XFrameworkServiceNames.IdentityServer,
            [XFrameworkServiceScopes.IdentityAdmin, " custom.scope ", XFrameworkServiceScopes.IdentityAdmin]);
        var second = await provider.GetTokenAsync(
            XFrameworkServiceNames.IdentityServer,
            ["custom.scope", XFrameworkServiceScopes.IdentityAdmin]);

        first.Should().Be("service-token");
        second.Should().Be("service-token");
        requestCount.Should().Be(1);
        requestUri.Should().Be("https://identity.test/api/service-identity/token");
        body.GetProperty("clientId").GetString().Should().Be(ClientId);
        body.GetProperty("clientSecret").GetString().Should().Be(ClientSecret);
        body.GetProperty("audience").GetString().Should().Be(XFrameworkServiceNames.IdentityServer);
        body.GetProperty("scopes").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            "custom.scope",
            XFrameworkServiceScopes.IdentityAdmin);
    }

    [Test]
    public async Task ServiceTokenProvider_ConcurrentEquivalentRequests_CoalesceToOneAcquisition()
    {
        var requestCount = 0;
        var acquisitionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAcquisition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = CreateFactory(async (_, ct) =>
        {
            Interlocked.Increment(ref requestCount);
            acquisitionStarted.TrySetResult();
            await releaseAcquisition.Task.WaitAsync(ct);
            return CreateTokenResponse("coalesced-token", DateTimeOffset.UtcNow.AddMinutes(5));
        });
        var provider = CreateServiceProvider(factory, CreateOptions());

        var requests = Enumerable.Range(0, 20)
            .Select(_ => provider.GetTokenAsync(
                    XFrameworkServiceNames.IdentityServer,
                    [XFrameworkServiceScopes.IdentityAdmin, "custom.scope"])
                .AsTask())
            .ToArray();
        await acquisitionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        requestCount.Should().Be(1);
        releaseAcquisition.TrySetResult();
        var tokens = await Task.WhenAll(requests);

        tokens.Should().OnlyContain(token => token == "coalesced-token");
        requestCount.Should().Be(1);
    }

    [Test]
    public async Task TokenProvider_ServerErrorContainingSensitiveValues_DoesNotLogThem()
    {
        const string issuedToken = "sensitive-issued-token";
        var logger = new RecordingLogger<IdentityServerBoltTransportTokenProvider>();
        using var factory = CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent($"{ClientSecret} {issuedToken}")
        }));
        var provider = CreateTransportProvider(factory, CreateOptions(), logger: logger);

        var call = async () => await provider.GetTokenAsync();
        await call.Should().ThrowAsync<InvalidOperationException>();

        logger.Messages.Should().NotContain(message =>
            message.Contains(ClientSecret, StringComparison.Ordinal) ||
            message.Contains(issuedToken, StringComparison.Ordinal));
    }

    [Test]
    public void ServiceTokenProvider_Constructors_DoNotDependOnBoltClient()
    {
        var parameterTypes = typeof(IdentityServerServiceTokenProvider)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        parameterTypes.Should().NotContain(typeof(BoltClient));
    }

    [TestCase("https://identity.test/api")]
    [TestCase("https://identity.test/api/")]
    public void ResolveAuthority_AuthorityContainsPath_ThrowsInvalidOperationException(string authority)
    {
        var options = CreateOptions();
        options.Authority = authority;

        var resolve = options.ResolveAuthority;

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be an origin without user information, a path, a query, or a fragment*");
    }

    [Test]
    public async Task DiAwareAccessTokenProvider_ScopedDisposableProvider_DisposesScopeAfterEachRequest()
    {
        var probe = new TokenProviderLifetimeProbe();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(probe);
        services.AddScoped<ScopedDisposableTokenProvider>();
        services.AddBoltClient(builder => builder
            .WithServer("wss://bolt.test/bolt")
            .WithAccessTokenProvider<ScopedDisposableTokenProvider>(
                static (provider, ct) => provider.GetTokenAsync(ct))
            .DisableAutoConnect());
        await using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<BoltClient>();
        var accessTokenProvider = GetAccessTokenProvider(client);

        var first = await accessTokenProvider(CancellationToken.None);
        var second = await accessTokenProvider(CancellationToken.None);

        first.Should().Be("scoped-token-1");
        second.Should().Be("scoped-token-2");
        probe.CreatedCount.Should().Be(2);
        probe.DisposedCount.Should().Be(2);
    }

    private static IdentityServerBoltTransportTokenProvider CreateTransportProvider(
        IHttpClientFactory factory,
        ServiceIdentityOptions options,
        TimeProvider? timeProvider = null,
        ILogger<IdentityServerBoltTransportTokenProvider>? logger = null) =>
        new(
            factory,
            Options.Create(options),
            timeProvider ?? TimeProvider.System,
            logger ?? NullLogger<IdentityServerBoltTransportTokenProvider>.Instance);

    private static IdentityServerServiceTokenProvider CreateServiceProvider(
        IHttpClientFactory factory,
        ServiceIdentityOptions options) =>
        new(
            factory,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<IdentityServerServiceTokenProvider>.Instance);

    private static ServiceIdentityOptions CreateOptions(
        int tokenAcquisitionTimeoutSeconds = 5,
        int refreshSkewSeconds = 30) => new()
    {
        Authority = "https://identity.test",
        ClientId = ClientId,
        GenerationId = "test-g0",
        ClientSecret = ClientSecret,
        TokenAcquisitionTimeoutSeconds = tokenAcquisitionTimeoutSeconds,
        TokenRefreshSkewSeconds = refreshSkewSeconds
    };

    private static StubHttpClientFactory CreateFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
        new(new DelegateHttpMessageHandler(handler));

    private static HttpResponseMessage CreateTokenResponse(string token, DateTimeOffset expiresAtUtc)
    {
        var json = JsonSerializer.Serialize(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresAtUtc
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static Func<CancellationToken, ValueTask<string?>> GetAccessTokenProvider(BoltClient client)
    {
        var configField = typeof(BoltClient).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
        configField.Should().NotBeNull();
        var options = configField!.GetValue(client).Should().BeOfType<BoltClientOptions>().Which;
        options.AccessTokenProvider.Should().NotBeNull();
        return options.AccessTokenProvider!;
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public List<string> RequestedClientNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedClientNames.Add(name);
            return _client;
        }

        public void Dispose()
        {
            _client.Dispose();
            handler.Dispose();
        }
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan amount) => utcNow = utcNow.Add(amount);
    }

    private sealed class TokenProviderLifetimeProbe
    {
        private int _createdCount;
        private int _disposedCount;

        public int CreatedCount => Volatile.Read(ref _createdCount);
        public int DisposedCount => Volatile.Read(ref _disposedCount);

        public int RecordCreated() => Interlocked.Increment(ref _createdCount);

        public void RecordDisposed() => Interlocked.Increment(ref _disposedCount);
    }

    private sealed class ScopedDisposableTokenProvider(TokenProviderLifetimeProbe probe) : IAsyncDisposable
    {
        private readonly int _instanceId = probe.RecordCreated();

        public ValueTask<string?> GetTokenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult<string?>($"scoped-token-{_instanceId}");
        }

        public ValueTask DisposeAsync()
        {
            probe.RecordDisposed();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
