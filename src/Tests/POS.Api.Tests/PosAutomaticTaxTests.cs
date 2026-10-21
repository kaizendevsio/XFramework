using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Api.Features;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;

namespace POS.Api.Tests;

[TestFixture]
public sealed class PosAutomaticTaxTests
{
    [TestCase(true, 9, 80, 7.2)]
    [TestCase(false, 9, 80, 0)]
    [TestCase(true, 5, 0.1, 0.01)]
    [TestCase(true, 9, 0, 0)]
    public void CalculateTax_DiscountedBase_RoundsOnceAwayFromZero(bool enabled, decimal rate, decimal taxableBase, decimal expected) =>
        new TenantFinanceSettings { TaxEnabled = enabled, TaxRatePercent = rate }.CalculateTax(taxableBase).Should().Be(expected);

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    [TestCase(-1, 0)]
    public async Task WriteServices_ClientTaxOverride_RejectedBeforePersistence(decimal header, decimal line)
    {
        var resolver = new Resolver();
        var sales = new PosSalesService(null!, null!, null!, resolver, NullLogger<PosSalesService>.Instance, null!, null!);
        var carts = new PosCartService(null!, null!, sales, resolver, NullLogger<PosCartService>.Instance, null!, null!);
        var sale = await sales.CheckoutAsync(new CheckoutPosSaleRequest
        { TaxAmount = header, Lines = [new() { TaxAmount = line }] }, CancellationToken.None);
        var create = await carts.CreateAsync(new CreatePosCartRequest
        { TaxAmount = header, Lines = [new() { TaxAmount = line }] }, CancellationToken.None);
        var update = await carts.UpdateAsync(new UpdatePosCartRequest
        { TaxAmount = header, Lines = [new() { TaxAmount = line }] }, CancellationToken.None);
        sale.StatusCode.Should().Be(400);
        create.StatusCode.Should().Be(400);
        update.StatusCode.Should().Be(400);
        sale.Message.Should().Contain("cannot be supplied");
    }

    [Test]
    public void Validators_PositiveClientTaxOverride_IsRejected()
    {
        new CreatePosCartValidator().Validate(new CreatePosCartRequest { TaxAmount = 1 }).Errors.Should().Contain(x => x.PropertyName == "TaxAmount");
        new UpdatePosCartValidator().Validate(new UpdatePosCartRequest { TaxAmount = 1 }).Errors.Should().Contain(x => x.PropertyName == "TaxAmount");
        new PosCartLineValidator().Validate(new PosCartLineRequest { TaxAmount = 1 }).Errors.Should().Contain(x => x.PropertyName == "TaxAmount");
        new CheckoutPosSaleValidator().Validate(new CheckoutPosSaleRequest { TaxAmount = 1 }).Errors.Should().Contain(x => x.PropertyName == "TaxAmount");
    }

    [Test]
    public void AutomaticRefunds_FullyDiscountedItemHasNoTax_WhileLegacyAllocationIsUnchanged()
    {
        var paid = new PosSaleLine { Id = Guid.NewGuid(), LineNumber = 1, Quantity = 1, UnitPrice = 100, LineTotal = 100 };
        var free = new PosSaleLine { Id = Guid.NewGuid(), LineNumber = 2, Quantity = 1, UnitPrice = 100, DiscountAmount = 100, LineTotal = 0 };
        var sale = new PosSale { TaxAmount = 10, Lines = [paid, free] };
        var legacy = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        legacy[free.Id].Should().Be(new PosSaleLineRefundAllocation(5, 5));
        sale.UsesAutomaticTax = true;
        var automatic = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        automatic[free.Id].Should().Be(new PosSaleLineRefundAllocation(0, 0));
        automatic[paid.Id].Should().Be(new PosSaleLineRefundAllocation(10, 110));
        PosServiceHelpers.ToSaleReceiptResponse(sale).Lines.Single(x => x.Id == free.Id).RefundAmount.Should().Be(0);
        sale.DiscountAmount = 20;
        sale.TaxAmount = 8;
        automatic = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        automatic[free.Id].Should().Be(new PosSaleLineRefundAllocation(0, 0));
        automatic[paid.Id].Should().Be(new PosSaleLineRefundAllocation(8, 88));
    }

    [TestCase(6.03)]
    [TestCase(0.03)]
    public void PartialRefunds_SixSuccessiveReturns_CapRoundedAmountsAtRemainingSnapshot(decimal refund)
    {
        var original = new PosSaleLineRefundAllocation(0.03m, refund);
        decimal refundedTax = 0, refundedAmount = 0;
        for (var returned = 0; returned < 6; returned++)
        {
            var allocation = PosServiceHelpers.BuildPartialReturnAllocation(original, 6, returned, refundedTax, refundedAmount, 1);
            allocation.TaxAmount.Should().BeGreaterThanOrEqualTo(0);
            allocation.RefundAmount.Should().BeGreaterThanOrEqualTo(0);
            refundedTax += allocation.TaxAmount;
            refundedAmount += allocation.RefundAmount;
            refundedTax.Should().BeLessThanOrEqualTo(original.TaxAmount);
            refundedAmount.Should().BeLessThanOrEqualTo(original.RefundAmount);
        }
        refundedTax.Should().Be(original.TaxAmount);
        refundedAmount.Should().Be(original.RefundAmount);
    }

    [Test]
    public void AutomaticRefunds_SixEqualLinesAtHalfPercent_NeverAllocateNegativeTax()
    {
        var settings = new TenantFinanceSettings { TaxEnabled = true, TaxRatePercent = 0.5m };
        var sale = new PosSale
        {
            UsesAutomaticTax = true, TaxAmount = settings.CalculateTax(6),
            Lines = Enumerable.Range(1, 6).Select(i => new PosSaleLine
            { Id = Guid.NewGuid(), LineNumber = i, Quantity = 1, UnitPrice = 1, LineTotal = 1 }).ToList()
        };
        sale.TaxAmount.Should().Be(0.03m);
        var allocations = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        allocations.Values.Should().OnlyContain(x => x.TaxAmount >= 0 && x.RefundAmount >= 1);
        allocations.Values.Sum(x => x.TaxAmount).Should().Be(0.03m);
        allocations.Values.Sum(x => x.RefundAmount).Should().Be(6.03m);
        PosServiceHelpers.BuildSaleRefundAllocations(sale).Should().BeEquivalentTo(allocations);
        sale.UsesAutomaticTax = false;
        var legacy = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        legacy[sale.Lines.Last().Id].TaxAmount.Should().Be(-0.02m, "unmarked historical sales retain their original allocation");
    }

    [Test]
    public void AutomaticRefunds_RoundingConservesHeaderDiscountTaxAndPartialReturnRemainders()
    {
        var sale = new PosSale
        {
            UsesAutomaticTax = true, DiscountAmount = 0.01m, TaxAmount = 0.01m,
            Lines = Enumerable.Range(1, 3).Select(i => new PosSaleLine
            { Id = Guid.NewGuid(), LineNumber = i, Quantity = 3, UnitPrice = 0.01m, LineTotal = 0.03m }).ToList()
        };
        var allocations = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        allocations.Values.Sum(x => x.TaxAmount).Should().Be(0.01m);
        allocations.Values.Sum(x => x.RefundAmount).Should().Be(0.09m);
        allocations.Values.Should().OnlyContain(x => x.TaxAmount >= 0 && x.RefundAmount >= 0);
        var original = allocations.Values.First();
        var first = PosServiceHelpers.BuildPartialReturnAllocation(original, 3, 0, 0, 0, 1);
        var rest = PosServiceHelpers.BuildPartialReturnAllocation(original, 3, 1, first.TaxAmount, first.RefundAmount, 2);
        (first.RefundAmount + rest.RefundAmount).Should().Be(original.RefundAmount);
        (first.TaxAmount + rest.TaxAmount).Should().Be(original.TaxAmount);
    }

    [Test]
    public void Refunds_AutomaticTaxSnapshot_IsUnaffectedBySubsequentRateChange()
    {
        var settings = new TenantFinanceSettings { TaxEnabled = true, TaxRatePercent = 10 };
        var sale = new PosSale
        {
            UsesAutomaticTax = true, DiscountAmount = 10, TaxAmount = settings.CalculateTax(80),
            Lines = [new() { Id = Guid.NewGuid(), LineNumber = 1, Quantity = 3, UnitPrice = 30, LineTotal = 90 }]
        };
        settings.TaxRatePercent = 25;
        var allocation = PosServiceHelpers.BuildSaleRefundAllocations(sale).Values.Single();
        allocation.Should().Be(new PosSaleLineRefundAllocation(8, 88));
        var first = PosServiceHelpers.BuildPartialReturnAllocation(allocation, 3, 0, 0, 0, 1);
        var remaining = PosServiceHelpers.BuildPartialReturnAllocation(allocation, 3, 1, first.TaxAmount, first.RefundAmount, 2);
        (first.TaxAmount + remaining.TaxAmount).Should().Be(8);
        (first.RefundAmount + remaining.RefundAmount).Should().Be(88);
    }

    private sealed class Resolver : IPosRequestContextResolver
    {
        public Result<PosRequestContext> Resolve(RequestBase request, Guid? requestCredentialId = null) =>
            Result<PosRequestContext>.Success(new(Guid.NewGuid(), requestCredentialId, request.Metadata, true, false));
    }
}
