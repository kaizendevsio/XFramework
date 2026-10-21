using System.Net;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Features.POS;

namespace Portal.E2ETests;

[TestFixture]
public sealed class PosCustomerCreationTests
{
    [Test]
    public async Task CreateAsync_UsesIdentityWrappersAndReturnsCredential_WithoutWalletCalls()
    {
        var tenant = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var wrapper = Wrapper(tenant, identityId);
        wrapper.Setup(x => x.CreateCredential(It.IsAny<CreateCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<CredentialAdministrationResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new() { Id = credentialId, TenantId = tenant, IdentityInfoId = identityId,
                    UserName = "customer", UserAlias = "Customer", IsEnabled = true }
            });
        var result = await new PosCustomerCreation(wrapper.Object).CreateAsync(tenant, "Customer", "customer",
            "test-password", new() { RequestedTenantId = tenant });
        result.IsSuccess.Should().BeTrue();
        result.Response!.Id.Should().Be(credentialId);
        result.Response.TenantId.Should().Be(tenant);
        wrapper.Verify(x => x.CreateIdentity(It.Is<CreateIdentityRequest>(r => r.Metadata.RequestedTenantId == tenant),
            It.IsAny<CancellationToken>()), Times.Once);
        wrapper.Verify(x => x.CreateCredential(It.Is<CreateCredentialRequest>(r => r.IdentityInfoId == identityId),
            It.IsAny<CancellationToken>()), Times.Once);
        wrapper.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateAsync_CredentialFailure_RetryReusesIdentity_AndRejectsTenantSwitch()
    {
        var tenant = Guid.NewGuid();
        var wrapper = Wrapper(tenant, Guid.NewGuid());
        wrapper.Setup(x => x.CreateCredential(It.IsAny<CreateCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<CredentialAdministrationResponse> { HttpStatusCode = HttpStatusCode.Conflict });
        var creation = new PosCustomerCreation(wrapper.Object);
        for (var attempt = 0; attempt < 2; attempt++)
            (await creation.CreateAsync(tenant, "Customer", "customer", "test-password",
                new() { RequestedTenantId = tenant })).IsSuccess.Should().BeFalse();
        var otherTenant = Guid.NewGuid();
        (await creation.CreateAsync(otherTenant, "Customer", "customer", "test-password",
            new() { RequestedTenantId = otherTenant })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrapper.Verify(x => x.CreateIdentity(It.IsAny<CreateIdentityRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        wrapper.Verify(x => x.CreateCredential(It.IsAny<CreateCredentialRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task CreateAsync_ForeignIdentityResponse_DoesNotCreateCredential()
    {
        var tenant = Guid.NewGuid();
        var wrapper = Wrapper(Guid.NewGuid(), Guid.NewGuid());
        var result = await new PosCustomerCreation(wrapper.Object).CreateAsync(tenant, "Customer", "customer",
            "test-password", new() { RequestedTenantId = tenant });
        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrapper.Verify(x => x.CreateCredential(It.IsAny<CreateCredentialRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IIdentityServerServiceWrapper> Wrapper(Guid tenant, Guid identityId)
    {
        var wrapper = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        wrapper.Setup(x => x.CreateIdentity(It.IsAny<CreateIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<IdentityAdministrationResponse>
            {
                HttpStatusCode = HttpStatusCode.OK, Response = new() { Id = identityId, TenantId = tenant }
            });
        return wrapper;
    }
}
