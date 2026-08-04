using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class IdentityServerSigningKeyProviderTests
{
    [Test]
    public async Task GetSigningKeysAsync_ConcurrentUnknownKids_UseOneBoundedRefreshAndSelectLocally()
    {
        var handler = new SigningKeyHandler();
        var provider = new IdentityServerSigningKeyProvider(
            new TestHttpClientFactory(handler),
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.local",
                AllowInsecureHttp = true,
                ClientId = XFrameworkServiceNames.Communications,
                SigningKeyCacheMinutes = 15
            }));

        var unknownResults = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(index => provider.GetSigningKeysAsync($"unknown-{index}")));
        var knownResult = await provider.GetSigningKeysAsync(SigningKeyHandler.KnownKeyId);

        Assert.That(unknownResults, Has.All.Empty);
        Assert.That(knownResult.Select(static key => key.KeyId), Is.EqualTo(new[] { SigningKeyHandler.KnownKeyId }));
        Assert.That(handler.RequestCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetSigningKeysAsync_NewRotationKid_ForcesOneThrottledRefresh()
    {
        var handler = new SigningKeyHandler();
        var provider = new IdentityServerSigningKeyProvider(
            new TestHttpClientFactory(handler),
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.local",
                AllowInsecureHttp = true,
                ClientId = XFrameworkServiceNames.Communications,
                SigningKeyCacheMinutes = 15
            }));

        var original = await provider.GetSigningKeysAsync(SigningKeyHandler.KnownKeyId);
        Assert.That(original.Select(static key => key.KeyId), Does.Contain(SigningKeyHandler.KnownKeyId));
        handler.Rotate();

        var rotated = await provider.GetSigningKeysAsync(SigningKeyHandler.RotatedKeyId);
        var unknown = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(index => provider.GetSigningKeysAsync($"unknown-after-rotation-{index}")));

        Assert.That(rotated.Select(static key => key.KeyId), Does.Contain(SigningKeyHandler.RotatedKeyId));
        Assert.That(unknown, Has.All.Empty);
        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetSigningKeysAsync_ConcurrentUnknownKidsDuringOutage_UseOneFailedRefresh()
    {
        var handler = new SigningKeyHandler();
        var provider = new IdentityServerSigningKeyProvider(
            new TestHttpClientFactory(handler),
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.local",
                AllowInsecureHttp = true,
                ClientId = XFrameworkServiceNames.Communications,
                SigningKeyCacheMinutes = 15
            }));
        await provider.GetSigningKeysAsync(SigningKeyHandler.KnownKeyId);
        handler.FailRequests = true;

        var attempts = Enumerable.Range(0, 32).Select(async index =>
        {
            try
            {
                await provider.GetSigningKeysAsync($"unknown-outage-{index}");
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        });
        var errors = await Task.WhenAll(attempts);

        Assert.That(errors, Has.None.Null);
        Assert.That(errors.Count(static error => error is HttpRequestException), Is.EqualTo(1));
        Assert.That(errors.Count(static error => error is InvalidOperationException), Is.EqualTo(31));
        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task IsAcceptedAsync_NewCredentialGeneration_ForcesPolicyRefresh()
    {
        var handler = new SigningKeyHandler();
        var provider = new IdentityServerSigningKeyProvider(
            new TestHttpClientFactory(handler),
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.local",
                AllowInsecureHttp = true,
                ClientId = XFrameworkServiceNames.Communications,
                SigningKeyCacheMinutes = 15
            }));

        await provider.GetSigningKeysAsync(SigningKeyHandler.KnownKeyId);
        handler.RotateGeneration();

        var accepted = await provider.IsAcceptedAsync(
            XFrameworkServiceNames.Portal,
            "generation-2");
        var retired = await provider.IsAcceptedAsync(
            XFrameworkServiceNames.Portal,
            "generation-1");

        Assert.That(accepted, Is.True);
        Assert.That(retired, Is.False);
        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task IsAcceptedAsync_PreviouslyAcceptedGeneration_IsRejectedAfterPolicyRetiresIt()
    {
        var handler = new SigningKeyHandler();
        var provider = new IdentityServerSigningKeyProvider(
            new TestHttpClientFactory(handler),
            Options.Create(new ServiceIdentityOptions
            {
                Authority = "http://identity.local",
                AllowInsecureHttp = true,
                ClientId = XFrameworkServiceNames.Communications,
                SigningKeyCacheMinutes = 15,
                CredentialGenerationCacheSeconds = 0
            }));

        var initiallyAccepted = await provider.IsAcceptedAsync(
            XFrameworkServiceNames.Portal,
            "generation-1");
        handler.RotateGeneration();
        var acceptedAfterRetirement = await provider.IsAcceptedAsync(
            XFrameworkServiceNames.Portal,
            "generation-1");

        Assert.That(initiallyAccepted, Is.True);
        Assert.That(acceptedAfterRetirement, Is.False);
        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class SigningKeyHandler : HttpMessageHandler
    {
        public const string KnownKeyId = "known-key";
        public const string RotatedKeyId = "rotated-key";
        private int _requestCount;
        private int _rotated;
        private int _generationRotated;

        public int RequestCount => Volatile.Read(ref _requestCount);
        public bool FailRequests { get; set; }

        public void Rotate() => Volatile.Write(ref _rotated, 1);
        public void RotateGeneration() => Volatile.Write(ref _generationRotated, 1);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            if (FailRequests)
                throw new HttpRequestException("IdentityServer unavailable");
            var keyId = Volatile.Read(ref _rotated) == 0 ? KnownKeyId : RotatedKeyId;
            var response = new ServiceSigningKeysResponse
            {
                Keys =
                [
                    new ServiceSigningKeyResponse
                    {
                        KeyId = keyId,
                        Algorithm = "RS256",
                        PublicKeyPem = "public-key",
                        CreatedAtUtc = DateTime.UtcNow,
                        ActivatedAtUtc = DateTime.UtcNow,
                        IsActive = true
                    }
                ],
                CredentialGenerationsByClient = new Dictionary<string, List<string>>(StringComparer.Ordinal)
                {
                    [XFrameworkServiceNames.Portal] =
                    [Volatile.Read(ref _generationRotated) == 0 ? "generation-1" : "generation-2"]
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }
}
