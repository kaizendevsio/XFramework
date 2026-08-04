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

        var policy = new InvocationAuthorizationPolicy
        {
            ActorRequirement = ActorRequirement.None,
            TenantAccessMode = TenantAccessMode.Tenantless,
            RequiredServiceScopes = ["scope.required"],
            AllowedServiceCallers = [XFrameworkServiceNames.Portal]
        };
        var result = await authorizer.AuthorizeAsync(
            new InvocationCredentials(null, "token"),
            new RequestMetadata(),
            context,
            policy);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(resolver.ExpectedAudience, Is.EqualTo(XFrameworkServiceNames.Communications));
        Assert.That(resolver.Policy, Is.EqualTo(policy));
    }

    [Test]
    public async Task AuthorizeAsync_TokenCallerDoesNotMatchTransportSender_ReturnsForbidden()
    {
        var contextStore = new TestContextStore();
        var authorizer = CreateAuthorizer(
            new StubResolver(SuccessfulInvocation(XFrameworkServiceNames.Portal)),
            contextStore);
        var context = new BoltInboundRequestContext(
            Guid.NewGuid(),
            BoltCodec.Fnv1aHash(XFrameworkServiceNames.Wallets.ToSha256()));

        var result = await authorizer.AuthorizeAsync(
            new InvocationCredentials(null, "token"),
            new RequestMetadata(),
            context,
            TenantlessServicePolicy());

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
        Assert.That(contextStore.Current, Is.Null);
    }

    [Test]
    public async Task AuthorizeAsync_SenderMismatch_DoesNotValidateActorToken()
    {
        var actorProvider = new CountingActorProvider();
        var serviceProvider = new StubServiceIdentityProvider(ServiceIdentityValidationResult.Success(
            new TrustedServiceIdentity(
                XFrameworkServiceNames.Portal,
                XFrameworkServiceNames.Communications,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "generation")));
        var authorizer = new BoltServiceInvocationAuthorizer(
            new TrustedInvocationResolver(actorProvider, serviceProvider),
            serviceProvider,
            new TestContextStore(),
            Options.Create(new ServiceIdentityOptions
            {
                ClientId = XFrameworkServiceNames.Communications
            }));

        var result = await authorizer.AuthorizeAsync(
            new InvocationCredentials("actor-token", "service-token"),
            new RequestMetadata(),
            new BoltInboundRequestContext(
                Guid.NewGuid(),
                BoltCodec.Fnv1aHash(XFrameworkServiceNames.Wallets.ToSha256())),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Required,
                TenantAccessMode = TenantAccessMode.ActorTenant
            });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
        Assert.That(actorProvider.CallCount, Is.Zero);
    }

    [Test]
    public async Task AuthorizeAsync_ExplicitAnonymousPolicy_PublishesTenantlessContext()
    {
        var anonymousContext = new TrustedInvocationContext(null, null, null, null, Guid.NewGuid());
        var contextStore = new TestContextStore();
        var authorizer = CreateAuthorizer(
            new StubResolver(TrustedInvocationResult.Success(anonymousContext)),
            contextStore);

        var result = await authorizer.AuthorizeAsync(
            new InvocationCredentials(null, null),
            new RequestMetadata(),
            new BoltInboundRequestContext(Guid.NewGuid(), 0),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.Tenantless,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            });

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(contextStore.Current, Is.SameAs(anonymousContext));
    }

    [Test]
    public async Task Resolver_MissingRequiredScope_ReturnsForbidden()
    {
        var resolver = CreateResolver(new ServiceTokenValidationResult(
            true,
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.other" },
            null,
            null));

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "token"),
            new RequestMetadata(),
            TenantlessServicePolicy(requiredScopes: ["scope.required"]),
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task Resolver_DisallowedCaller_ReturnsForbidden()
    {
        var resolver = CreateResolver(new ServiceTokenValidationResult(
            true,
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null,
            null));

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "token"),
            new RequestMetadata(),
            TenantlessServicePolicy(allowedCallers: [XFrameworkServiceNames.Wallets]),
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [TestCase(401)]
    [TestCase(503)]
    public async Task Resolver_ServiceIdentityFailure_PreservesStatusCode(int statusCode)
    {
        var resolver = new TrustedInvocationResolver(
            new RejectingActorProvider(),
            new StubServiceIdentityProvider(ServiceIdentityValidationResult.Failure("validation failed", statusCode)));

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "invalid"),
            new RequestMetadata(),
            TenantlessServicePolicy(),
            XFrameworkServiceNames.Communications);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(statusCode));
    }

    private static BoltServiceInvocationAuthorizer CreateAuthorizer(
        ITrustedInvocationResolver resolver,
        ITrustedInvocationContextStore? contextStore = null) =>
        new(
            resolver,
            new StubServiceIdentityProvider(ServiceIdentityValidationResult.Success(
                new TrustedServiceIdentity(
                    XFrameworkServiceNames.Portal,
                    XFrameworkServiceNames.Communications,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.required" },
                    "generation"))),
            contextStore ?? new TestContextStore(),
            Options.Create(new ServiceIdentityOptions
            {
                ClientId = XFrameworkServiceNames.Communications
            }));

    private static TrustedInvocationResolver CreateResolver(ServiceTokenValidationResult validation) =>
        new(
            new RejectingActorProvider(),
            new ServiceIdentityProvider(new StubTokenValidator(validation)));

    private static InvocationAuthorizationPolicy TenantlessServicePolicy(
        IReadOnlyCollection<string>? requiredScopes = null,
        IReadOnlyCollection<string>? allowedCallers = null) =>
        new()
        {
            ActorRequirement = ActorRequirement.None,
            TenantAccessMode = TenantAccessMode.Tenantless,
            RequiredServiceScopes = requiredScopes ?? [],
            AllowedServiceCallers = allowedCallers ?? []
        };

    private static TrustedInvocationResult SuccessfulInvocation(string caller) =>
        TrustedInvocationResult.Success(new TrustedInvocationContext(
            Actor: null,
            Service: new TrustedServiceIdentity(
                caller,
                XFrameworkServiceNames.Communications,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.required" },
                "generation"),
            EffectiveTenantId: null,
            RequestedTargetTenantId: null,
            CorrelationId: Guid.NewGuid()));

    private sealed class StubResolver(TrustedInvocationResult result) : ITrustedInvocationResolver
    {
        public string? ExpectedAudience { get; private set; }
        public InvocationAuthorizationPolicy? Policy { get; private set; }

        public Task<TrustedInvocationResult> ResolveAsync(
            InvocationCredentials credentials,
            RequestMetadata metadata,
            InvocationAuthorizationPolicy policy,
            string expectedAudience,
            CancellationToken ct = default)
        {
            ExpectedAudience = expectedAudience;
            Policy = policy;
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

    private sealed class StubServiceIdentityProvider(ServiceIdentityValidationResult result) : IServiceIdentityProvider
    {
        public Task<ServiceIdentityValidationResult> ValidateAsync(
            string token,
            string expectedAudience,
            CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class RejectingActorProvider : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(ActorIdentityValidationResult.Failure("unexpected actor"));
    }

    private sealed class CountingActorProvider : IActorIdentityProvider
    {
        public int CallCount { get; private set; }

        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(ActorIdentityValidationResult.Failure("unexpected actor validation"));
        }
    }

    private sealed class TestContextStore : ITrustedInvocationContextStore
    {
        public TrustedInvocationContext? Current { get; private set; }
        public void Set(TrustedInvocationContext context) => Current = context;
    }
}
