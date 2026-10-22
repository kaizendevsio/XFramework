using System.Net;
using System.Reflection;
using BlazorBlueprint.Components;
using FluentAssertions;
using Inventario.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Portal.Features.Inventario.Pages;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
public sealed class ReorderRuleSaveTests
{
    [TestCase(null)]
    [TestCase("")]
    public async Task Save_EmptySupplier_ReachesTenantScopedUpdateWithoutCrashing(string? supplier)
    {
        var tenantId = Guid.NewGuid();
        var rule = new InventoryReorderRule { Id = Guid.NewGuid(), TenantId = tenantId };
        var tenant = Mock.Of<IPortalTenantContext>(x => x.SelectedTenantId == tenantId);
        var wrapper = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        UpdateInventoryReorderRuleRequest? submitted = null;
        wrapper.Setup(x => x.UpdateInventoryReorderRule(It.IsAny<UpdateInventoryReorderRuleRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateInventoryReorderRuleRequest, CancellationToken>((request, _) => submitted = request)
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.Conflict });
        var services = new ServiceCollection().AddLogging();
        services.AddBlazorBlueprintComponents();
        using var provider = services.BuildServiceProvider();
        var page = new ReorderRuleDetail();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ReorderRuleDetail).GetProperty("TenantFilter", flags)!.SetValue(page, tenant);
        typeof(ReorderRuleDetail).GetProperty("Inventario", flags)!.SetValue(page, wrapper.Object);
        typeof(ReorderRuleDetail).GetProperty("ToastService", flags)!.SetValue(page, provider.GetRequiredService<ToastService>());
        typeof(ReorderRuleDetail).GetField("_rule", flags)!.SetValue(page, rule);
        typeof(ReorderRuleDetail).GetField("_editPreferredSupplier", flags)!.SetValue(page, supplier);
        typeof(ReorderRuleDetail).GetField("_editReorderQuantity", flags)!.SetValue(page, 2m);

        await (Task)typeof(ReorderRuleDetail).GetMethod("SaveMaintenance", flags)!.Invoke(page, null)!;

        submitted.Should().NotBeNull();
        submitted!.Metadata.RequestedTenantId.Should().Be(tenantId);
        submitted.Id.Should().Be(rule.Id);
        submitted.PreferredSupplier.Should().Be(supplier);
        submitted.ReorderQuantity.Should().Be(2m);
        typeof(ReorderRuleDetail).GetField("_maintenanceSaving", flags)!.GetValue(page).Should().Be(false);
    }
}
