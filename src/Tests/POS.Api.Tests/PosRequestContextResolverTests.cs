using POS.Api.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosRequestContextResolverTests
{
    [Test]
    public void Resolve_TrustedActorTenantMismatch_ReturnsForbidden()
    {
        var trustedTenantId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                RequestedTenantId = Guid.NewGuid()
            }
        };
        var resolver = new PosRequestContextResolver(
            TrustedContext(actor: Actor(trustedTenantId, Guid.NewGuid())));

        var result = resolver.Resolve(request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public void Resolve_TargetCashierMismatchForNonPrivilegedUser_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                RequestedTenantId = tenantId
            }
        };
        var resolver = new PosRequestContextResolver(
            TrustedContext(actor: Actor(tenantId, actorCredentialId)));

        var result = resolver.Resolve(request, requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Test]
    public void Resolve_ActorPlusServiceContext_UsesActorAuthority()
    {
        var tenantId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var request = new RequestBase
        {
            Metadata = new RequestMetadata
            {
                RequestedTenantId = tenantId
            }
        };
        var resolver = new PosRequestContextResolver(
            TrustedContext(
                actor: Actor(tenantId, actorCredentialId),
                service: Service()));

        var result = resolver.Resolve(request);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.TenantId.Should().Be(tenantId);
        result.Data.ActorCredentialId.Should().Be(actorCredentialId);
        result.Data.IsTrustedInternal.Should().BeFalse();
        result.Data.IsPrivilegedActor.Should().BeFalse();
        result.Data.TrustedServiceName.Should().Be(XFrameworkServiceNames.Portal);
        request.Metadata.RequestedTenantId.Should().Be(tenantId);
    }

    [Test]
    public void Resolve_ActorPlusServiceCannotOperateAsAnotherCashier()
    {
        var tenantId = Guid.NewGuid();
        var actorCredentialId = Guid.NewGuid();
        var resolver = new PosRequestContextResolver(
            TrustedContext(
                actor: Actor(tenantId, actorCredentialId),
                service: Service()));

        var result = resolver.Resolve(
            new RequestBase { Metadata = new RequestMetadata() },
            requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("cannot operate");
    }

    [Test]
    public void Resolve_ServiceOnlyContext_RemainsTrustedInternal()
    {
        var tenantId = Guid.NewGuid();
        var resolver = new PosRequestContextResolver(
            TrustedContext(service: Service(), effectiveTenantId: tenantId));

        var result = resolver.Resolve(
            new RequestBase { Metadata = new RequestMetadata() },
            requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.IsTrustedInternal.Should().BeTrue();
        result.Data.IsPrivilegedActor.Should().BeTrue();
    }

    private static ITrustedInvocationContextAccessor TrustedContext(
        TrustedActorIdentity? actor = null,
        TrustedServiceIdentity? service = null,
        Guid? effectiveTenantId = null) =>
        new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            actor,
            service,
            effectiveTenantId ?? actor?.TenantId,
            null,
            Guid.NewGuid()));

    private static TrustedActorIdentity Actor(Guid tenantId, Guid credentialId) => new(
        credentialId,
        Guid.NewGuid(),
        tenantId,
        Guid.NewGuid(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "test-generation",
        DateTimeOffset.UtcNow.AddMinutes(5));

    private static TrustedServiceIdentity Service() => new(
        XFrameworkServiceNames.Portal,
        XFrameworkServiceNames.Pos,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "test-service-generation");

    private sealed class TestTrustedInvocationContextAccessor(TrustedInvocationContext context)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => context;
    }
}
