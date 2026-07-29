using Bolt.Client;
using Bolt.Protocol;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class BoltServiceInvocationAuthorizerTests
{
    [Test]
    public async Task AuthorizeAsync_MatchingTokenCallerAndSender_AllowsInvocation()
    {
        var resolver = new StubResolver(SuccessfulInvocation(XFrameworkServiceNames.Portal));
        var authorizer = CreateAuthorizer(resolver);
        var context = new BoltInboundRequestContext(
            Guid.NewGuid(),
            BoltCodec.Fnv1aHash(XFrameworkServiceNames.Portal.ToSha256()));

        var result = await authorizer.AuthorizeAsync(
            new RequestMetadata { ServiceAccessToken = "token" },
            context,
            ["scope.required"],
            [XFrameworkServiceNames.Portal]);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(resolver.ExpectedAudience, Is.EqualTo(XFrameworkServiceNames.Communications));
        Assert.That(resolver.RequiredScopes, Is.EquivalentTo(new[] { "scope.required" }));
        Assert.That(resolver.AllowedCallers, Is.EquivalentTo(new[] { XFrameworkServiceNames.Portal }));
        Assert.That(resolver.RequireTenant, Is.False);
    }

    [Test]
    public async Task AuthorizeAsync_TokenCallerDoesNotMatchTransportSender_ReturnsForbidden()
    {
        var resolver = new StubResolver(SuccessfulInvocation(XFrameworkServiceNames.Portal));
        var authorizer = CreateAuthorizer(resolver);
        var context = new BoltInboundRequestContext(
            Guid.NewGuid(),
            BoltCodec.Fnv1aHash(XFrameworkServiceNames.Wallets.ToSha256()));

        var result = await authorizer.AuthorizeAsync(
            new RequestMetadata { ServiceAccessToken = "token" },
            context);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task Resolver_MissingRequiredScope_ReturnsForbidden()
    {
        var validator = new StubTokenValidator(new ServiceTokenValidationResult(
            true,
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.other" },
            null,
            null));
        var resolver = new TrustedServiceInvocationResolver(validator);

        var result = await resolver.ResolveAsync(
            new RequestMetadata { ServiceAccessToken = "token" },
            XFrameworkServiceNames.Communications,
            ["scope.required"],
            requireTenant: false);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task Resolver_DisallowedCaller_ReturnsForbidden()
    {
        var validator = new StubTokenValidator(new ServiceTokenValidationResult(
            true,
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null,
            null));
        var resolver = new TrustedServiceInvocationResolver(validator);

        var result = await resolver.ResolveAsync(
            new RequestMetadata { ServiceAccessToken = "token" },
            XFrameworkServiceNames.Communications,
            allowedCallers: [XFrameworkServiceNames.Wallets],
            requireTenant: false);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task Resolver_InvalidToken_ReturnsUnauthorized()
    {
        var resolver = new TrustedServiceInvocationResolver(
            new StubTokenValidator(ServiceTokenValidationResult.Failure("invalid")));

        var result = await resolver.ResolveAsync(
            new RequestMetadata { ServiceAccessToken = "invalid" },
            XFrameworkServiceNames.Communications,
            requireTenant: false);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task Resolver_SigningKeyInfrastructureUnavailable_ReturnsServiceUnavailable()
    {
        var resolver = new TrustedServiceInvocationResolver(
            new StubTokenValidator(ServiceTokenValidationResult.Unavailable("keys unavailable")));

        var result = await resolver.ResolveAsync(
            new RequestMetadata { ServiceAccessToken = "unverified" },
            XFrameworkServiceNames.Communications,
            requireTenant: false);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(503));
    }

    private static BoltServiceInvocationAuthorizer CreateAuthorizer(ITrustedServiceInvocationResolver resolver) =>
        new(
            resolver,
            Options.Create(new ServiceIdentityOptions
            {
                ClientId = XFrameworkServiceNames.Communications
            }));

    private static TrustedServiceInvocationResult SuccessfulInvocation(string caller) =>
        TrustedServiceInvocationResult.Success(new TrustedServiceInvocation(
            caller,
            XFrameworkServiceNames.Communications,
            null,
            null,
            new RequestMetadata { ServiceAccessToken = "token" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.required" }));

    private sealed class StubResolver(TrustedServiceInvocationResult result)
        : ITrustedServiceInvocationResolver
    {
        public string? ExpectedAudience { get; private set; }
        public IReadOnlyCollection<string>? RequiredScopes { get; private set; }
        public IReadOnlyCollection<string>? AllowedCallers { get; private set; }
        public bool RequireTenant { get; private set; }

        public Task<TrustedServiceInvocationResult> ResolveAsync(
            RequestMetadata? metadata,
            string expectedAudience,
            IReadOnlyCollection<string>? requiredScopes = null,
            IReadOnlyCollection<string>? allowedCallers = null,
            bool requireTenant = true,
            CancellationToken ct = default)
        {
            ExpectedAudience = expectedAudience;
            RequiredScopes = requiredScopes;
            AllowedCallers = allowedCallers;
            RequireTenant = requireTenant;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTokenValidator(ServiceTokenValidationResult result) : IServiceTokenValidator
    {
        public Task<ServiceTokenValidationResult> ValidateAsync(
            string? token,
            string expectedAudience,
            IReadOnlyCollection<string>? requiredScopes = null,
            CancellationToken ct = default) =>
            Task.FromResult(result);
    }
}
