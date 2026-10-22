using System.Net;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using Microsoft.Playwright;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;

namespace Portal.E2ETests;

public sealed partial class InventarioSetupWizardE2ETests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    [Category("Area:ProductWorkflow")]
    public async Task ProductWorkflow_EnableStock_RequiresConfirmationAndPreservesTenant(bool confirm, bool fail)
    {
        PrepareProductWorkflow();
        _state.CanManage = true;
        _state.DisabledWorkflowFeatures.Add("movements");
        var stamp = Guid.NewGuid();
        ReadRows<Tenant>(_data, () => [new Tenant { Id = _state.ActiveTenant, ConcurrencyStamp = stamp }], applyFilters: true);
        ReadRows<TenantModuleFeature>(_data, () => [new TenantModuleFeature
        {
            TenantId = _state.ActiveTenant, ModuleKey = "inventario", SubFeatureKey = "catalog",
            DisplayName = "Existing catalog name", Description = "Existing catalog description"
        }], applyFilters: true);
        SetTenantModuleFeaturesRequest? sent = null;
        _stockFeatureIdentity.Setup(x => x.SetTenantModuleFeatures(It.IsAny<SetTenantModuleFeaturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SetTenantModuleFeaturesRequest request, CancellationToken _) =>
            {
                sent = request;
                if (!fail) _state.DisabledWorkflowFeatures.Clear();
                return new CmdResponse { HttpStatusCode = fail ? HttpStatusCode.Conflict : HttpStatusCode.OK };
            });
        try
        {
            await OpenWorkflowProduct();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Stock", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Enable stock tracking", Exact = true }).ClickAsync();
            var dialog = Page.GetByRole(AriaRole.Alertdialog);
            await Expect(dialog).ToBeVisibleAsync();
            sent.Should().BeNull();
            await Screenshot("stock-enablement-confirm-1920");
            await dialog.GetByRole(AriaRole.Button, new() { Name = confirm ? "Enable stock tracking" : "Cancel", Exact = true }).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            if (confirm)
            {
                if (fail)
                    await Expect(Page.GetByText("Stock tracking could not be enabled. Refresh and try again.", new() { Exact = true })).ToBeVisibleAsync();
                else
                    await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add stock", Exact = true })).ToBeVisibleAsync();
                sent.Should().NotBeNull();
                sent!.TenantId.Should().Be(_state.FirstTenant);
                sent.Metadata.RequestedTenantId.Should().Be(_state.FirstTenant);
                sent.ExpectedConcurrencyStamp.Should().Be(stamp);
                sent.Features.Should().OnlyContain(x => x.IsEnabled && x.ModuleKey == "inventario");
                sent.Features.Select(x => x.SubFeatureKey).Should().BeEquivalentTo("", "catalog", "warehousing", "stock_balances", "movements");
                sent.Features.Single(x => x.SubFeatureKey == "catalog").DisplayName.Should().Be("Existing catalog name");
            }
            else sent.Should().BeNull();
            AssertNoDirectStockWrites();
            _errors.Should().BeEmpty();
        }
        finally { _stockFeatureIdentity.Reset(); }
    }
}
