using System;
using System.Collections.Generic;
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
public sealed class TrustedServiceTargetContextInitializerTests
{
    [Test]
    public async Task EstablishAsync_UsesServiceTargetPolicyAndStoresResolvedContext()
    {
        var tenantId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var context = new TrustedInvocationContext(
            null,
            new TrustedServiceIdentity(
                XFrameworkServiceNames.IdentityServer,
                XFrameworkServiceNames.Storage,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    XFrameworkServiceScopes.StorageWrite,
                    XFrameworkServiceScopes.TenantTarget
                },
                "generation"),
            tenantId,
            tenantId,
            requestId);
        var tokenProvider = new RecordingServiceTokenProvider();
        var resolver = new RecordingResolver(TrustedInvocationResult.Success(context));
        var store = new RecordingContextStore();
        var initializer = new TrustedServiceTargetContextInitializer(tokenProvider, resolver, store);

        var result = await initializer.EstablishAsync(
            tenantId,
            XFrameworkServiceNames.Storage,
            [XFrameworkServiceScopes.StorageWrite],
            XFrameworkServiceNames.IdentityServer,
            requestId);

        result.IsSuccess.Should().BeTrue(result.Error);
        tokenProvider.Audience.Should().Be(XFrameworkServiceNames.Storage);
        tokenProvider.Scopes.Should().BeEquivalentTo(
            XFrameworkServiceScopes.StorageWrite,
            XFrameworkServiceScopes.TenantTarget);
        resolver.Credentials!.ActorAccessToken.Should().BeNull();
        resolver.Credentials.ServiceAccessToken.Should().Be(RecordingServiceTokenProvider.Token);
        resolver.Metadata!.RequestedTenantId.Should().Be(tenantId);
        resolver.Metadata.RequestId.Should().Be(requestId);
        resolver.ExpectedAudience.Should().Be(XFrameworkServiceNames.Storage);
        resolver.Policy!.ActorRequirement.Should().Be(ActorRequirement.None);
        resolver.Policy.TenantAccessMode.Should().Be(TenantAccessMode.ServiceTargetTenant);
        resolver.Policy.RequireServiceIdentity.Should().BeTrue();
        resolver.Policy.RequiredServiceScopes.Should().BeEquivalentTo(
            XFrameworkServiceScopes.StorageWrite,
            XFrameworkServiceScopes.TenantTarget);
        resolver.Policy.AllowedServiceCallers.Should().Equal(XFrameworkServiceNames.IdentityServer);
        store.Current.Should().BeSameAs(context);
    }

    [Test]
    public async Task EstablishAsync_WhenResolutionFails_DoesNotSetContext()
    {
        var resolver = new RecordingResolver(
            TrustedInvocationResult.Failure("Service caller is not allowed.", 403));
        var store = new RecordingContextStore();
        var initializer = new TrustedServiceTargetContextInitializer(
            new RecordingServiceTokenProvider(),
            resolver,
            store);

        var result = await initializer.EstablishAsync(
            Guid.NewGuid(),
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.BoltService],
            XFrameworkServiceNames.IdentityServer);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        store.Current.Should().BeNull();
    }

    [Test]
    public void AddTrustedInvocationSecurity_PreservesReplacementInitializer()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITrustedServiceTargetContextInitializer, ReplacementInitializer>();

        services.AddTrustedInvocationSecurity();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITrustedServiceTargetContextInitializer>()
            .Should().BeOfType<ReplacementInitializer>();
    }

    [Test]
    public void AddTrustedInvocationSecurity_PreservesReplacementIdentityProviders()
    {
        var services = new ServiceCollection();
        services.AddScoped<IActorIdentityProvider, ReplacementActorIdentityProvider>();
        services.AddSingleton<IServiceIdentityProvider, ReplacementServiceIdentityProvider>();

        services.AddTrustedInvocationSecurity();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IActorIdentityProvider>()
            .Should().BeOfType<ReplacementActorIdentityProvider>();
        scope.ServiceProvider.GetRequiredService<IServiceIdentityProvider>()
            .Should().BeOfType<ReplacementServiceIdentityProvider>();
    }

    private sealed class RecordingServiceTokenProvider : IServiceTokenProvider
    {
        public const string Token = "issued-service-token";

        public string? Audience { get; private set; }
        public IReadOnlyCollection<string> Scopes { get; private set; } = [];

        public ValueTask<string> GetTokenAsync(
            string audience,
            IReadOnlyCollection<string>? scopes = null,
            CancellationToken ct = default)
        {
            Audience = audience;
            Scopes = scopes ?? [];
            return ValueTask.FromResult(Token);
        }
    }

    private sealed class RecordingResolver(TrustedInvocationResult result) : ITrustedInvocationResolver
    {
        public InvocationCredentials? Credentials { get; private set; }
        public RequestMetadata? Metadata { get; private set; }
        public InvocationAuthorizationPolicy? Policy { get; private set; }
        public string? ExpectedAudience { get; private set; }

        public Task<TrustedInvocationResult> ResolveAsync(
            InvocationCredentials credentials,
            RequestMetadata metadata,
            InvocationAuthorizationPolicy policy,
            string expectedAudience,
            CancellationToken ct = default)
        {
            Credentials = credentials;
            Metadata = metadata;
            Policy = policy;
            ExpectedAudience = expectedAudience;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingContextStore : ITrustedInvocationContextStore
    {
        public TrustedInvocationContext? Current { get; private set; }
        public void Set(TrustedInvocationContext context) => Current = context;
    }

    private sealed class ReplacementInitializer : ITrustedServiceTargetContextInitializer
    {
        public Task<TrustedInvocationResult> EstablishAsync(
            Guid targetTenantId,
            string audience,
            IReadOnlyCollection<string> requiredServiceScopes,
            string allowedServiceCaller,
            Guid? correlationId = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReplacementActorIdentityProvider : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(
            string token,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class ReplacementServiceIdentityProvider : IServiceIdentityProvider
    {
        public Task<ServiceIdentityValidationResult> ValidateAsync(
            string token,
            string expectedAudience,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
