using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;

namespace POS.IntegrationTests;

public sealed partial class PosReportingPostgresTests
{
    [Test]
    public async Task ProductReport_GroupsAllLinesBeforePaging_SeparatesVariantsCurrenciesAndTenants()
    {
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        var currency = Guid.NewGuid(); var secondCurrency = Guid.NewGuid();
        var product = Guid.NewGuid(); var variant = Guid.NewGuid();
        var register = Register(tenant, currency);
        PosSaleLine Line(PosSale sale, Guid productId, string name, int number, Guid? variation = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = sale.TenantId, SaleId = sale.Id, LineNumber = number,
            ProductId = productId, ProductVariationId = variation, ProductName = name,
            VariantName = variation.HasValue ? "Large" : null, SKU = "SKU-" + number,
            Quantity = 2, UnitPrice = 10, LineTotal = 20, DiscountAmount = 2, TaxAmount = 2,
            WarehouseId = sale.WarehouseId, LocationId = sale.LocationId, CreatedAt = Start,
            IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
        };
        await using (var foreignDb = Context(other))
        {
            var foreignRegister = Register(other, currency);
            var foreignSale = Sale(foreignRegister, currency, Start, 18);
            foreignDb.AddRange(foreignRegister, foreignSale, Line(foreignSale, product, "Widget", 1));
            await foreignDb.SaveChangesAsync();
        }
        await using var db = Context(tenant);
        var sale = Sale(register, currency, Start, 10000);
        var otherCurrencySale = Sale(register, secondCurrency, Start, 18);
        var failed = Sale(register, currency, Start, 18); failed.Status = PosSaleStatus.PaymentFailed;
        var deleted = Sale(register, currency, Start, 18); deleted.IsDeleted = true;
        var before = Sale(register, currency, Start.AddTicks(-1), 18);
        var after = Sale(register, currency, Start.AddDays(2), 18);
        db.AddRange(register, sale, otherCurrencySale, failed, deleted, before, after);
        db.AddRange(Enumerable.Range(1, 501).Select(i => Line(sale, product, "Widget", i)));
        db.Add(Line(sale, product, "Widget", 502, variant));
        db.AddRange(Enumerable.Range(503, 26).Select(i => Line(sale, Guid.NewGuid(), $"Other {i}", i)));
        db.AddRange(new[] { otherCurrencySale, failed, deleted, before, after }.Select(s => Line(s, product, "Widget", 1)));
        var deletedLine = Line(sale, product, "Widget", 529); deletedLine.IsDeleted = true;
        db.Add(deletedLine);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = Service(db, tenant, Guid.NewGuid());
        var request = new GetPosProductSalesReportRequest
        {
            Metadata = new() { RequestedTenantId = tenant }, FromUtc = Start, ToUtcExclusive = Start.AddDays(2), Take = 20
        };
        var first = await service.GetProductSalesReportAsync(request, CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Message);
        first.Data!.TotalCount.Should().Be(29);
        first.Data.Rows.Should().HaveCount(20);
        var second = (await service.GetProductSalesReportAsync(request with { Skip = 20 }, CancellationToken.None)).Data!;
        second.Rows.Should().HaveCount(9);
        first.Data.Rows.Intersect(second.Rows).Should().BeEmpty();
        var products = first.Data.Rows.Concat(second.Rows).ToList();
        var aggregate = products.Single(x => x.ProductId == product && x.ProductVariationId == null && x.CurrencyId == currency);
        aggregate.Quantity.Should().Be(1002);
        aggregate.SalesAmount.Should().Be(9018);
        products.Single(x => x.ProductVariationId == variant).Quantity.Should().Be(2);
        products.Single(x => x.CurrencyId == secondCurrency).SalesAmount.Should().Be(18);
        db.ChangeTracker.Entries().Should().BeEmpty();
        var filtered = (await service.GetProductSalesReportAsync(request with { ProductFilter = "Widget", ProductFilterOperator = "Equals" }, CancellationToken.None)).Data!;
        filtered.TotalCount.Should().Be(3);
        var excluded = (await service.GetProductSalesReportAsync(request with { ProductFilter = "Widget", ProductFilterOperator = "NotContains" }, CancellationToken.None)).Data!;
        excluded.TotalCount.Should().Be(26);
        excluded.Rows.Should().OnlyContain(x => x.ProductId != product);
        var empty = (await service.GetProductSalesReportAsync(request with { FromUtc = Start.AddDays(5), ToUtcExclusive = Start.AddDays(6) }, CancellationToken.None)).Data!;
        empty.TotalCount.Should().Be(0); empty.Rows.Should().BeEmpty();
    }
}
