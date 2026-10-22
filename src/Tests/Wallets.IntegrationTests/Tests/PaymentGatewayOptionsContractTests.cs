using System.Reflection;
using Wallets.Api.Features.PaymentGateways.GetOptions;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace GeneratedAuthorizationContractTests.Wallets;

[TestFixture]
public sealed class PaymentGatewayOptionsContractTests
{
    [Test]
    public void OptionsEndpoint_RestAndBolt_RequireActorAndCapabilityProtectedDelegation()
    {
        var method = typeof(GetWalletPaymentGatewayOptionsEndpoint).GetMethod("Handle")!;
        var rest = method.GetCustomAttribute<MapPostAttribute>()!;
        var bolt = method.GetCustomAttribute<BoltHandlerAttribute>()!;
        rest.RequireAuthorization.Should().BeTrue();
        rest.AllowAnonymous.Should().BeFalse();
        rest.ActorRequirement.Should().Be(ActorRequirement.Required);
        bolt.ActorRequirement.Should().Be(ActorRequirement.Required);
        rest.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        bolt.TenantAccessMode.Should().Be(TenantAccessMode.DelegatedTenant);
        rest.RequiredActorCapabilities.Should().Equal(WalletAuthorizationCapabilities.ReportingView);
        bolt.RequiredActorCapabilities.Should().Equal(rest.RequiredActorCapabilities!);
        rest.RequiredCrossTenantActorCapabilities.Should().Equal(XFrameworkActorCapabilities.IdentityTenantsManage);
        bolt.RequiredCrossTenantActorCapabilities.Should().Equal(rest.RequiredCrossTenantActorCapabilities!);
    }

    [Test]
    public void OptionsResponse_ExposesOnlyDisplayCategoryAndFeeFields()
    {
        typeof(WalletPaymentGatewayOptionResponse).GetProperties().Select(x => x.Name).Should().BeEquivalentTo(
            "Id", "Name", "Description", "GatewayCategoryId", "CategoryName", "ServiceCharge", "ConvenienceFee", "Discount");
    }
}
