using FluentAssertions;
using Inventario.Api.Features.Stock.PostBasic;
using NUnit.Framework;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class PostBasicProductStockValidatorTests
{
    private readonly PostBasicProductStockValidator _validator = new();
    private static PostBasicProductStockRequest Valid() => new()
    {
        ProductId = Guid.NewGuid(), Quantity = 1.2345m, IdempotencyKey = "receipt-1"
    };

    [Test]
    public void Validate_BaseOrVariantReceipt_AcceptsPositiveQuantity()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
        _validator.Validate(Valid() with { ProductVariationId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("0.00001")]
    [TestCase("100000000000000")]
    public void Validate_InvalidQuantity_RejectsZeroNegativeOrUnrepresentableQuantity(string value) =>
        _validator.Validate(Valid() with { Quantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture) })
            .IsValid.Should().BeFalse();

    [Test]
    public void Validate_InvalidIdsKeyOrReason_RejectsInvalidShape()
    {
        foreach (var request in new[]
        {
            Valid() with { ProductId = Guid.Empty }, Valid() with { ProductVariationId = Guid.Empty },
            Valid() with { IdempotencyKey = " " }, Valid() with { IdempotencyKey = new string('k', 201) },
            Valid() with { Reason = new string('r', 1001) }
        }) _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
