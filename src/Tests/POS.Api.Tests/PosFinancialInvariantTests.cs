using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosFinancialInvariantTests
{
    [Test]
    public void BuildSaleRefundAllocations_ReconcilesLineAndHeaderAdjustmentsExactly()
    {
        var sale = new PosSale
        {
            DiscountAmount = 20,
            TaxAmount = 10,
            Lines =
            [
                new PosSaleLine
                {
                    Id = Guid.NewGuid(),
                    LineNumber = 1,
                    Quantity = 2,
                    UnitPrice = 50,
                    DiscountAmount = 10,
                    TaxAmount = 5,
                    LineTotal = 95
                },
                new PosSaleLine
                {
                    Id = Guid.NewGuid(),
                    LineNumber = 2,
                    Quantity = 1,
                    UnitPrice = 100,
                    LineTotal = 100
                }
            ]
        };

        var allocations = PosServiceHelpers.BuildSaleRefundAllocations(sale);

        allocations[sale.Lines.ElementAt(0).Id].Should().Be(new PosSaleLineRefundAllocation(10, 90));
        allocations[sale.Lines.ElementAt(1).Id].Should().Be(new PosSaleLineRefundAllocation(5, 95));
        allocations.Values.Sum(item => item.RefundAmount).Should().Be(185);
    }

    [Test]
    public void BuildPartialReturnAllocation_FinalReturnReceivesRoundingRemainder()
    {
        var original = new PosSaleLineRefundAllocation(1, 10);

        var first = PosServiceHelpers.BuildPartialReturnAllocation(original, 3, 0, 0, 0, 1);
        var final = PosServiceHelpers.BuildPartialReturnAllocation(
            original,
            3,
            1,
            first.TaxAmount,
            first.RefundAmount,
            2);

        first.Should().Be(new PosSaleLineRefundAllocation(0.33m, 3.33m));
        final.Should().Be(new PosSaleLineRefundAllocation(0.67m, 6.67m));
        (first.RefundAmount + final.RefundAmount).Should().Be(10);
        (first.TaxAmount + final.TaxAmount).Should().Be(1);
    }
}
