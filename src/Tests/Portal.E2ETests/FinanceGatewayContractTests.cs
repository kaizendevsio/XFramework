using System.Net;
using FluentAssertions;
using Moq;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using Wallets.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Portal.Features.Finance.Services;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public class FinanceGatewayContractTests
{
    [Test]
    public async Task GatewayPicker_UsesCapturedTenantAndLoadsFollowingPages()
    {
        var tenantId = Guid.NewGuid();
        var wrapper = new Mock<IWalletsServiceWrapper>();
        var requests = new List<WalletPaymentGatewayOptionsRequest>();
        wrapper.Setup(x => x.WalletPaymentGatewayOptions(It.IsAny<WalletPaymentGatewayOptionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WalletPaymentGatewayOptionsRequest request, CancellationToken _) =>
            {
                requests.Add(request);
                return new CmdResponse<List<WalletPaymentGatewayOptionResponse>>
                {
                    HttpStatusCode = HttpStatusCode.OK,
                    Response = request.Page == 1
                        ? Enumerable.Range(0, 100).Select(i => new WalletPaymentGatewayOptionResponse { Id = Guid.NewGuid(), Name = $"Gateway {i}" }).ToList()
                        : []
                };
            });
        var service = new WalletsAdminBackendContractService(wrapper.Object,
            Mock.Of<IPortalTenantContext>(), new RequestMetadata());

        var options = await service.GetPaymentGatewaysAsync(tenantId);

        options.Should().HaveCount(100).And.OnlyContain(x => x.TenantId == tenantId && x.IsEnabled);
        requests.Select(x => x.Page).Should().Equal(1, 2);
        requests.Should().OnlyContain(x => x.Metadata.RequestedTenantId == tenantId && x.PageSize == 100);
    }

    [Test]
    public async Task GatewayPicker_DoesNotTurnAccessFailureIntoAnEmptyList()
    {
        var wrapper = new Mock<IWalletsServiceWrapper>();
        wrapper.Setup(x => x.WalletPaymentGatewayOptions(It.IsAny<WalletPaymentGatewayOptionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<List<WalletPaymentGatewayOptionResponse>> { HttpStatusCode = HttpStatusCode.Forbidden });
        var service = new WalletsAdminBackendContractService(wrapper.Object,
            Mock.Of<IPortalTenantContext>(), new RequestMetadata());

        var action = () => service.GetPaymentGatewaysAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Payment gateways could not be loaded*");
    }

    [TestCase("Requester cannot resolve their own wallet case")]
    [TestCase("Requester cannot approve their own maker-checker request")]
    public void MakerCheckerFailure_ExplainsSeparateActorRequirement(string message) =>
        WalletsPortalDisplayService.WorkflowFailureMessage(message, "Failed")
            .Should().Be("A different authorized user must approve or reject this request.");

    [Test]
    public void UnexpectedFailure_DoesNotExposeBackendDetails() =>
        WalletsPortalDisplayService.WorkflowFailureMessage("Internal connection details", "Could not complete")
            .Should().Be("Could not complete");
}
