using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using Inventario.Api.Features.Transactions.ProductSales;

namespace Inventario.Api.Tests;

public sealed class InventoryProductSalesTests
{
    [TestCase("anonymous", 401)]
    [TestCase("denied-manager", 403)]
    [TestCase("foreign", 403)]
    [TestCase("disabled", 403)]
    [TestCase("invalid", 400)]
    public async Task ProductSales_DeniesBeforeDatabaseAccess(string scenario, int expected)
    {
        var tenant = Guid.NewGuid();
        var actor = new TestTrustedInvocationContextAccessor(scenario == "anonymous" ? null : tenant,
            capabilities: new HashSet<string>(scenario == "denied-manager" ? [XFrameworkActorCapabilities.IdentityTenantsManage] : ["inventario.transactions:view"]));
        var gates = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        gates.Setup(x => x.EnsureEnabledAsync(tenant, "inventario", "transactions", It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "disabled" ? Result.Forbidden("disabled") : Result.Success());
        var service = new InventoryProductSalesService(new Mock<DbContext>(MockBehavior.Strict).Object, actor,
            gates.Object, NullLogger<InventoryProductSalesService>.Instance);
        var result = await service.GetAsync(new() { Metadata = new() { RequestedTenantId = scenario == "foreign" ? Guid.NewGuid() : tenant }, Take = 101 }, CancellationToken.None);
        result.StatusCode.Should().Be(expected); result.Data.Should().BeNull();
    }

    [Test]
    public void ProductSales_TransportsRequireTransactionsView_NotReportingOrPOS()
    {
        var method = typeof(GetInventoryProductSalesEndpoint).GetMethod("Handle")!;
        method.GetCustomAttribute<BoltHandlerAttribute>()!.RequiredActorCapabilities.Should().Equal("inventario.transactions:view");
        method.GetCustomAttribute<MapEndpointAttribute>()!.RequiredActorCapabilities.Should().Equal("inventario.transactions:view");
    }

    [Test]
    public void ProductSales_ValidatorBoundsDatesAndPaging()
    {
        var validator = new GetInventoryProductSalesValidator();
        var request = new GetInventoryProductSalesRequest();
        validator.Validate(request).IsValid.Should().BeTrue();
        validator.Validate(request with { Take = 101 }).IsValid.Should().BeFalse();
        validator.Validate(request with { Skip = -1 }).IsValid.Should().BeFalse();
        validator.Validate(request with { ToUtcExclusive = request.FromUtc.AddDays(367) }).IsValid.Should().BeFalse();
        validator.Validate(request with { ProductFilterOperator = "NotContains" }).IsValid.Should().BeTrue();
        validator.Validate(request with { ProductFilterOperator = "unknown" }).IsValid.Should().BeFalse();
    }
}
