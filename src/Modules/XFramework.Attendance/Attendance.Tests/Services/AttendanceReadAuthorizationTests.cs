using Attendance.Api.Features.Reads.ContextOverview;
using Attendance.Domain.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace Attendance.Tests.Services;

[TestFixture]
public sealed class AttendanceReadAuthorizationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();

    [Test]
    public async Task ReadPolicy_ValidPortalActorForOwnTenant_IsAllowed()
    {
        var result = await ResolveAsync(CreateActor([AttendanceAuthorizationCapabilities.View]));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Context!.EffectiveTenantId.Should().Be(TenantId);
    }

    [Test]
    public async Task ReadPolicy_ServiceOnlyInvocation_IsDenied()
    {
        var result = await ResolveAsync(actor: null);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task ReadPolicy_ActorWithoutAttendanceView_IsDenied()
    {
        var result = await ResolveAsync(CreateActor([]));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ReadPolicy_DifferentRequestedTenant_IsDenied()
    {
        var result = await ResolveAsync(
            CreateActor([AttendanceAuthorizationCapabilities.View]),
            requestedTenantId: OtherTenantId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task ReadPolicy_WrongCallerOrMissingScope_IsDenied(
        bool allowedCaller,
        bool hasReadScope)
    {
        var service = CreateService(
            allowedCaller ? XFrameworkServiceNames.Portal : XFrameworkServiceNames.Wallets,
            hasReadScope ? [XFrameworkServiceScopes.AttendanceRead] : []);

        var result = await ResolveAsync(
            CreateActor([AttendanceAuthorizationCapabilities.View]),
            service);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task ReadRoute_DisabledAttendanceFeature_IsDeniedBeforeCapabilityLookup()
    {
        var featureService = new DenyingFeatureService();
        var capabilityService = new RecordingCapabilityService();
        var context = new TrustedInvocationContext(
            CreateActor([AttendanceAuthorizationCapabilities.View]),
            CreateService(XFrameworkServiceNames.Portal, [XFrameworkServiceScopes.AttendanceRead]),
            TenantId,
            TenantId,
            Guid.NewGuid());
        var gate = new TrustedInvocationFeatureGate(
            new TenantModuleFeatureGateOptions().RequireFeature("attendance", "/api/attendance"),
            featureService,
            capabilityService,
            new FixedContextAccessor(context),
            NullLogger<TrustedInvocationFeatureGate>.Instance);
        var map = typeof(GetAttendanceContextOverviewEndpoint)
            .GetMethod(nameof(GetAttendanceContextOverviewEndpoint.Handle))!
            .GetCustomAttributes(typeof(MapPostAttribute), false)
            .Cast<MapPostAttribute>()
            .Single();

        var result = await gate.EnsureAllowedAsync(map.Route, "POST", map.Capability);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        capabilityService.CallCount.Should().Be(0);
    }

    private static async Task<TrustedInvocationResult> ResolveAsync(
        TrustedActorIdentity? actor,
        TrustedServiceIdentity? service = null,
        Guid? requestedTenantId = null)
    {
        service ??= CreateService(
            XFrameworkServiceNames.Portal,
            [XFrameworkServiceScopes.AttendanceRead]);
        var resolver = new TrustedInvocationResolver(
            new FixedActorProvider(actor),
            new FixedServiceProvider(service));

        return await resolver.ResolveAsync(
            new InvocationCredentials(
                ServiceAccessToken: "service-token",
                ActorAccessToken: actor is null ? null : "actor-token"),
            new RequestMetadata { RequestedTenantId = requestedTenantId ?? TenantId },
            ReadPolicy(),
            XFrameworkServiceNames.Attendance);
    }

    private static InvocationAuthorizationPolicy ReadPolicy()
    {
        var attribute = typeof(GetAttendanceContextOverviewEndpoint)
            .GetMethod(nameof(GetAttendanceContextOverviewEndpoint.Handle))!
            .GetCustomAttributes(typeof(BoltHandlerAttribute), false)
            .Cast<BoltHandlerAttribute>()
            .Single();

        return new InvocationAuthorizationPolicy
        {
            ActorRequirement = attribute.ActorRequirement,
            TenantAccessMode = attribute.TenantAccessMode,
            RequiredServiceScopes = attribute.RequiredServiceScopes ?? [],
            AllowedServiceCallers = attribute.AllowedServiceCallers ?? [],
            RequiredActorCapabilities = attribute.RequiredActorCapabilities ?? [],
            AllowAnonymous = attribute.AllowAnonymous
        };
    }

    private static TrustedActorIdentity CreateActor(IReadOnlyCollection<string> capabilities) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        TenantId,
        Guid.NewGuid(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase),
        "attendance-tests",
        DateTimeOffset.UtcNow.AddMinutes(5));

    private static TrustedServiceIdentity CreateService(
        string clientId,
        IReadOnlyCollection<string> scopes) => new(
        clientId,
        XFrameworkServiceNames.Attendance,
        scopes.ToHashSet(StringComparer.OrdinalIgnoreCase),
        "attendance-tests");

    private sealed class FixedActorProvider(TrustedActorIdentity? actor) : IActorIdentityProvider
    {
        public Task<ActorIdentityValidationResult> ValidateAsync(
            string token,
            CancellationToken ct = default) =>
            Task.FromResult(actor is null
                ? ActorIdentityValidationResult.Failure("Actor is unavailable.")
                : ActorIdentityValidationResult.Success(actor));
    }

    private sealed class FixedServiceProvider(TrustedServiceIdentity service) : IServiceIdentityProvider
    {
        public Task<ServiceIdentityValidationResult> ValidateAsync(
            string token,
            string expectedAudience,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceIdentityValidationResult.Success(service));
    }

    private sealed class FixedContextAccessor(TrustedInvocationContext context)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => context;
    }

    private sealed class DenyingFeatureService : ITenantModuleFeatureService
    {
        public Task<Result<bool>> IsEnabledAsync(
            Guid tenantId,
            string moduleKey,
            string? subFeatureKey = null,
            CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(false));

        public Task<Result> EnsureEnabledAsync(
            Guid tenantId,
            string moduleKey,
            string? subFeatureKey = null,
            CancellationToken ct = default) =>
            Task.FromResult(Result.Forbidden("Attendance is disabled."));

        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null)
        {
        }
    }

    private sealed class RecordingCapabilityService : ITenantCredentialCapabilityService
    {
        public int CallCount { get; private set; }

        public Task<Result<bool>> IsAllowedAsync(
            Guid tenantId,
            Guid credentialId,
            string moduleKey,
            string? subFeatureKey,
            string capabilityKey,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result<bool>.Success(true));
        }

        public Task<Result> EnsureAllowedAsync(
            Guid tenantId,
            Guid credentialId,
            string moduleKey,
            string? subFeatureKey,
            string capabilityKey,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result.Success());
        }
    }
}
