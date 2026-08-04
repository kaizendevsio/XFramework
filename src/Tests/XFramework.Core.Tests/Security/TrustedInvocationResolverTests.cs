using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.Security;

[TestFixture]
public sealed class TrustedInvocationResolverTests
{
    private static readonly Guid ActorTenantId = Guid.Parse("8d44eb70-2882-465f-8d50-d96681a54056");

    [Test]
    public async Task ActorTenant_WhenRequestedTenantDiffers_RejectsSpoofedTenant()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy(),
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ActorTenant_WhenRequestedTenantMatches_UsesValidatedActorTenant()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(ActorTenantId),
            new InvocationAuthorizationPolicy(),
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(ActorTenantId);
        result.Context.Actor!.CredentialId.Should().Be(Actor().CredentialId);
    }

    [Test]
    public async Task DelegatedTenant_WithoutRequiredCapability_IsForbidden()
    {
        var resolver = CreateResolver(actor: Actor());
        var targetTenantId = Guid.NewGuid();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                TenantAccessMode = TenantAccessMode.DelegatedTenant,
                RequiredActorCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "identity.tenants:manage"
                }
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task DelegatedTenant_SameTenantDoesNotRequireCrossTenantCapability()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(ActorTenantId),
            new InvocationAuthorizationPolicy
            {
                TenantAccessMode = TenantAccessMode.DelegatedTenant,
                RequiredCrossTenantActorCapabilities = ["identity.tenants:manage"]
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(ActorTenantId);
    }

    [Test]
    public async Task DelegatedTenant_EmptyRequestedTenant_IsRejected()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(Guid.Empty),
            new InvocationAuthorizationPolicy
            {
                TenantAccessMode = TenantAccessMode.DelegatedTenant,
                RequiredCrossTenantActorCapabilities = ["identity.tenants:manage"]
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task DelegatedTenant_WithRequiredCapability_UsesExplicitTarget()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver(actor: Actor(capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "identity.tenants:manage"
        }));

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                TenantAccessMode = TenantAccessMode.DelegatedTenant,
                RequiredCrossTenantActorCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "identity.tenants:manage"
                }
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(targetTenantId);
        result.Context.Actor!.TenantId.Should().Be(ActorTenantId);
    }

    [Test]
    public async Task ServiceTargetTenant_WithAuthorizedService_UsesRequestedTarget()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver(serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jobs.execute",
            XFrameworkServiceScopes.TenantTarget
        });

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "jobs.execute",
                    XFrameworkServiceScopes.TenantTarget
                },
                AllowedServiceCallers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "caller"
                }
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.Actor.Should().BeNull();
        result.Context.EffectiveTenantId.Should().Be(targetTenantId);
    }

    [Test]
    public async Task SmsAgentPolicy_WithAuthorizedSmsGatewayService_UsesRequestedTenant()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver(
            serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                XFrameworkServiceScopes.SmsGatewayAgent,
                XFrameworkServiceScopes.TenantTarget
            },
            serviceClientId: XFrameworkServiceNames.SmsGateway);

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(targetTenantId),
            SmsAgentPolicy(),
            XFrameworkServiceNames.SmsGateway);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(targetTenantId);
        result.Context.Actor.Should().BeNull();
    }

    [Test]
    public async Task SmsAgentPolicy_WithActorOrUnrelatedService_IsForbidden()
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            XFrameworkServiceScopes.SmsGatewayAgent,
            XFrameworkServiceScopes.TenantTarget
        };
        var actorResolver = CreateResolver(
            actor: Actor(),
            serviceScopes: scopes,
            serviceClientId: XFrameworkServiceNames.SmsGateway);
        var unrelatedResolver = CreateResolver(
            serviceScopes: scopes,
            serviceClientId: XFrameworkServiceNames.Portal);

        var actorResult = await actorResolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(ActorTenantId),
            SmsAgentPolicy(),
            XFrameworkServiceNames.SmsGateway);
        var unrelatedResult = await unrelatedResolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(ActorTenantId),
            SmsAgentPolicy(),
            XFrameworkServiceNames.SmsGateway);

        actorResult.IsSuccess.Should().BeFalse();
        actorResult.StatusCode.Should().Be(403);
        unrelatedResult.IsSuccess.Should().BeFalse();
        unrelatedResult.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ServiceTargetTenant_GenericOptionalOperation_RejectsAnonymousTenantSelection()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequireServiceIdentity = false
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task PublicTenantLookup_UsesExplicitTargetWithoutIdentity()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.PublicTenantLookup,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.Actor.Should().BeNull();
        result.Context.Service.Should().BeNull();
        result.Context.EffectiveTenantId.Should().Be(targetTenantId);
    }

    [Test]
    public async Task PublicTenantLookup_DoesNotElevateAccompanyingServiceIdentity()
    {
        var targetTenantId = Guid.NewGuid();
        var resolver = CreateResolver(serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            XFrameworkServiceScopes.BoltService
        });

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(targetTenantId),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.PublicTenantLookup,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.Actor.Should().BeNull();
        result.Context.Service.Should().NotBeNull();
        result.Context.EffectiveTenantId.Should().Be(targetTenantId);
    }

    [Test]
    public async Task PublicTenantLookup_WithActorAndNoRequestedTarget_UsesActorTenant()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", null),
            Metadata(null),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.PublicTenantLookup,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(ActorTenantId);
    }

    [Test]
    public async Task PublicTenantLookup_WithActorAndDifferentRequestedTarget_IsForbidden()
    {
        var resolver = CreateResolver(actor: Actor());

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", null),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.PublicTenantLookup,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ServiceTargetTenant_AnonymousPolicy_CannotSelectTenant()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Test]
    public async Task Tenantless_GenericOptionalOperation_RejectsAnonymousInvocation()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(null),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.Tenantless,
                RequireServiceIdentity = false
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task Tenantless_PublicDiscoveryPolicy_AllowsAnonymousInvocation()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(null),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.Tenantless,
                RequireServiceIdentity = false,
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().BeNull();
    }

    [Test]
    public async Task Tenantless_AnonymousPolicyWithServiceScope_IsRejectedAsInvalid()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(null),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.Tenantless,
                RequireServiceIdentity = false,
                RequiredServiceScopes = ["identity.admin"],
                AllowAnonymous = true
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Test]
    public async Task ServiceTargetTenant_WithoutDedicatedTenantTargetScope_IsForbidden()
    {
        var resolver = CreateResolver(serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jobs.execute"
        });

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = ["jobs.execute"],
                AllowedServiceCallers = ["caller"]
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ServiceTargetTenant_WithoutExplicitCallerAllowlist_IsForbidden()
    {
        var resolver = CreateResolver(serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            XFrameworkServiceScopes.TenantTarget
        });

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = [XFrameworkServiceScopes.TenantTarget]
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ServiceTargetTenant_ServiceOnlyOperationWithoutServiceIdentity_IsRejected()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, null),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequireServiceIdentity = true
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task ServiceTargetTenant_WithActorPresent_DoesNotLetServiceOverrideActorTenant()
    {
        var resolver = CreateResolver(
            actor: Actor(),
            serviceScopes: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "jobs.execute",
                XFrameworkServiceScopes.TenantTarget
            });

        var result = await resolver.ResolveAsync(
            new InvocationCredentials("actor-token", "service-token"),
            Metadata(Guid.NewGuid()),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.Optional,
                TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
                RequiredServiceScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "jobs.execute",
                    XFrameworkServiceScopes.TenantTarget
                },
                AllowedServiceCallers = ["caller"]
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ProtectedRequest_WithoutActor_IsRejected()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(ActorTenantId),
            new InvocationAuthorizationPolicy(),
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task TenantlessOperation_WithRequestedTenant_IsRejected()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(
            new InvocationCredentials(null, "service-token"),
            Metadata(ActorTenantId),
            new InvocationAuthorizationPolicy
            {
                ActorRequirement = ActorRequirement.None,
                TenantAccessMode = TenantAccessMode.Tenantless
            },
            "target");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Test]
    public async Task ServiceIdentityProvider_UsesServiceCredentialGenerationClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("client_id", "caller"),
            new Claim("client_credential_generation", "service-generation-2")
        ]));
        var provider = new ServiceIdentityProvider(new StubTokenValidator(
            new ServiceTokenValidationResult(
                true,
                "caller",
                "target",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                principal,
                null)));

        var result = await provider.ValidateAsync("token", "target");

        result.IsValid.Should().BeTrue();
        result.Identity!.GenerationId.Should().Be("service-generation-2");
    }

    [Test]
    public void TrustedContextStore_DoesNotAllowEstablishedIdentitiesToBeRemoved()
    {
        var services = new ServiceCollection();
        services.AddTrustedInvocationSecurity();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>();
        var actor = Actor();
        var service = new TrustedServiceIdentity(
            "caller",
            "target",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "service-generation");
        var correlationId = Guid.NewGuid();
        var context = new TrustedInvocationContext(
            actor,
            service,
            actor.TenantId,
            actor.TenantId,
            correlationId);
        store.Set(context);

        var setSameContext = () => store.Set(context);

        var removeActor = () => store.Set(new TrustedInvocationContext(
            Actor: null,
            service,
            actor.TenantId,
            actor.TenantId,
            correlationId));
        var removeService = () => store.Set(new TrustedInvocationContext(
            actor,
            Service: null,
            actor.TenantId,
            actor.TenantId,
            correlationId));

        setSameContext.Should().NotThrow();
        removeActor.Should().Throw<InvalidOperationException>();
        removeService.Should().Throw<InvalidOperationException>();
        store.Current!.Actor.Should().BeSameAs(actor);
        store.Current.Service.Should().BeSameAs(service);
    }

    [Test]
    public void TrustedIdentities_FreezeInputCollections()
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin" };
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "identity.read" };
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scope.read" };

        var actor = Actor(capabilities, roles);
        var service = new TrustedServiceIdentity("caller", "target", scopes, "generation");

        roles.Add("super-admin");
        capabilities.Add("identity.write");
        scopes.Add("scope.write");

        actor.Roles.Should().BeEquivalentTo(["admin"]);
        actor.Capabilities.Should().BeEquivalentTo(["identity.read"]);
        service.Scopes.Should().BeEquivalentTo(["scope.read"]);
    }

    private static TrustedInvocationResolver CreateResolver(
        TrustedActorIdentity? actor = null,
        IReadOnlySet<string>? serviceScopes = null,
        string serviceClientId = "caller") =>
        new(
            new StubActorProvider(actor),
            new StubServiceProvider(new TrustedServiceIdentity(
                serviceClientId,
                "target",
                serviceScopes ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "service-generation")));

    private static InvocationAuthorizationPolicy SmsAgentPolicy() => new()
    {
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes =
        [
            XFrameworkServiceScopes.SmsGatewayAgent,
            XFrameworkServiceScopes.TenantTarget
        ],
        AllowedServiceCallers = [XFrameworkServiceNames.SmsGateway]
    };

    private static RequestMetadata Metadata(Guid? requestedTenantId) => new()
    {
        RequestId = Guid.NewGuid(),
        RequestedTenantId = requestedTenantId
    };

    private static TrustedActorIdentity Actor(
        IReadOnlySet<string>? capabilities = null,
        IReadOnlySet<string>? roles = null) => new(
        Guid.Parse("ae49ff82-cc8e-4cca-a42f-0e2b3d32bb37"),
        Guid.Parse("d340548b-ed0a-497b-a63a-31c28e1307c4"),
        ActorTenantId,
        Guid.Parse("a6a42f7f-4de2-48d8-9efd-af413cfb65d5"),
        roles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        capabilities ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "actor-generation",
        DateTimeOffset.UtcNow.AddMinutes(10));

    private sealed class StubActorProvider(TrustedActorIdentity? actor) : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(actor is null
                ? ActorIdentityValidationResult.Failure("invalid actor")
                : ActorIdentityValidationResult.Success(actor));
    }

    private sealed class StubServiceProvider(TrustedServiceIdentity service) : IServiceIdentityProvider
    {
        public Task<ServiceIdentityValidationResult> ValidateAsync(
            string token,
            string expectedAudience,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceIdentityValidationResult.Success(service));
    }

    private sealed class StubTokenValidator(ServiceTokenValidationResult result) : IServiceTokenValidator
    {
        public Task<ServiceTokenValidationResult> ValidateAsync(
            string? token,
            string expectedAudience,
            IReadOnlyCollection<string>? requiredScopes = null,
            CancellationToken ct = default) => Task.FromResult(result);
    }
}
