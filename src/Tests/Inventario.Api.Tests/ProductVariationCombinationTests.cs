using FluentAssertions;
using MemoryPack;
using NUnit.Framework;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;
using Inventario.Api.Features.Variations.Create;
using Inventario.Api.Features.Variations.Update;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class ProductVariationCombinationTests
{
    [Test]
    public void Validate_MultipleDistinctTypesAndValues_AcceptsCombination()
    {
        ProductVariationCombination.Validate(Options()).Should().BeNull();
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(9)]
    public void Validate_InvalidNumberOfOptions_RejectsCombination(int count)
    {
        var options = Enumerable.Range(0, count).Select(_ => new ProductVariationOption
        {
            ProductVariationTypeId = Guid.NewGuid(), Value = "Value"
        }).ToList();
        ProductVariationCombination.Validate(options).Should().NotBeNull();
    }

    [Test]
    public void Validate_RepeatedType_RejectsCombination()
    {
        var options = Options();
        options[1] = options[1] with { ProductVariationTypeId = options[0].ProductVariationTypeId };
        ProductVariationCombination.Validate(options).Should().Contain("only once");
    }

    [Test]
    public void Validate_MissingTypeOrValue_RejectsCombination()
    {
        var options = Options();
        options[0] = options[0] with { ProductVariationTypeId = Guid.Empty };
        ProductVariationCombination.Validate(options).Should().NotBeNull();
        options = Options();
        options[0] = options[0] with { Value = " " };
        ProductVariationCombination.Validate(options).Should().NotBeNull();
        options[0] = options[0] with { Value = new string('x', 101) };
        ProductVariationCombination.Validate(options).Should().NotBeNull();
    }

    [Test]
    public void Validate_NullPayloadOrEntry_RejectsCombination()
    {
        ProductVariationCombination.Validate(null).Should().NotBeNull();
        ProductVariationCombination.Validate([null!, new()]).Should().NotBeNull();
    }

    [Test]
    public void Normalize_ReorderedOptions_TrimsValuesWithoutChangingSource()
    {
        var options = Options();
        options[0] = options[0] with { Value = "  Red / Blue: XL  " };
        var normalized = ProductVariationCombination.Normalize(options);
        normalized.Should().Equal(ProductVariationCombination.Normalize(options.AsEnumerable().Reverse()));
        normalized.Should().Contain(x => x.Value == "Red / Blue: XL");
        options[0].Value.Should().Be("  Red / Blue: XL  ");
    }

    [Test]
    public void MemoryPack_StructuredOptions_RoundTripsTypeIdentityAndValues()
    {
        var options = Options();
        options[0] = options[0] with { Value = "Red / Blue: XL; 1=2" };
        MemoryPackSerializer.Deserialize<List<ProductVariationOption>>(MemoryPackSerializer.Serialize(options))
            .Should().Equal(options);
    }

    private static List<ProductVariationOption> Options() =>
    [
        new() { ProductVariationTypeId = Guid.NewGuid(), Value = "Red" },
        new() { ProductVariationTypeId = Guid.NewGuid(), Value = "Large" }
    ];

    [Test]
    public void CreateKey_OrderCaseAndWhitespaceDifferences_HaveSameBoundedKey()
    {
        var options = Options();
        options[0] = options[0] with { Value = "Dark Blue" };
        var equivalent = options.Select(x => x with { Value = "  " + x.Value.ToUpperInvariant().Replace(" ", "\t  ") + " " })
            .Reverse().ToList();
        ProductVariationCombination.CreateKey(options).Should().HaveLength(64)
            .And.Be(ProductVariationCombination.CreateKey(equivalent));
        var different = options.ToList();
        different[0] = different[0] with { Value = "DarkBlue" };
        ProductVariationCombination.CreateKey(different).Should().NotBe(ProductVariationCombination.CreateKey(options));
        different = options.ToList();
        different[0] = different[0] with { ProductVariationTypeId = Guid.NewGuid() };
        ProductVariationCombination.CreateKey(different).Should().NotBe(ProductVariationCombination.CreateKey(options));
    }

    [Test]
    public void GetSellingPrice_ZeroPricedCombination_NeverFallsBackToChangedBasePrice()
    {
        var variant = new ProductVariation { Price = 0, AdditionalPrice = -10, CombinationOptions = Options() };
        variant.GetSellingPrice(99).Should().Be(0);
        variant.CombinationKey = ProductVariationCombination.CreateKey(variant.CombinationOptions);
        variant.CombinationOptions = null;
        variant.GetSellingPrice(99).Should().Be(0);
        variant.CombinationKey = null;
        variant.GetSellingPrice(99).Should().Be(89, "legacy untyped delta-only variants retain their fallback");
        variant.ProductVariationTypeId = Guid.NewGuid();
        variant.GetSellingPrice(99).Should().Be(0, "typed variants also use absolute zero");
    }

    [Test]
    public void Validators_CombinationAndLegacyShape_RejectAmbiguityAndRequireConcurrencyOnUpdate()
    {
        var create = new CreateProductVariationRequest { ProductId = Guid.NewGuid(), Name = "Red large", Price = 0, CombinationOptions = Options() };
        var validator = new CreateProductVariationValidator();
        validator.Validate(create).IsValid.Should().BeTrue();
        validator.Validate(create with { ProductVariationTypeId = Guid.NewGuid() }).IsValid.Should().BeFalse();
        validator.Validate(create with { CombinationOptions = [] }).IsValid.Should().BeFalse();
        validator.Validate(create with { Price = -1 }).IsValid.Should().BeFalse();
        validator.Validate(create with { Price = 1.001m }).IsValid.Should().BeFalse();
        validator.Validate(create with { CombinationOptions = null, ProductVariationTypeId = Guid.NewGuid() }).IsValid.Should().BeTrue();
        var update = new UpdateProductVariationRequest { ProductVariationId = Guid.NewGuid(), Name = create.Name, CombinationOptions = create.CombinationOptions };
        var updateValidator = new UpdateProductVariationValidator();
        updateValidator.Validate(update).IsValid.Should().BeFalse();
        updateValidator.Validate(update with { ExpectedConcurrencyStamp = Guid.Empty }).IsValid.Should().BeFalse();
        updateValidator.Validate(update with { ExpectedConcurrencyStamp = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Test]
    public void MemoryPack_EntityAndCommands_RoundTripCombinationAndConcurrency()
    {
        var options = Options();
        var entity = new ProductVariation { Id = Guid.NewGuid(), CombinationOptions = options, CombinationKey = ProductVariationCombination.CreateKey(options), Price = 0 };
        var copy = MemoryPackSerializer.Deserialize<ProductVariation>(MemoryPackSerializer.Serialize(entity))!;
        copy.CombinationOptions.Should().Equal(options);
        copy.CombinationKey.Should().Be(entity.CombinationKey);
        copy.Price.Should().Be(0);
        var create = new CreateProductVariationRequest { ProductId = Guid.NewGuid(), Name = "Red large", CombinationOptions = options };
        MemoryPackSerializer.Deserialize<CreateProductVariationRequest>(MemoryPackSerializer.Serialize(create))!.CombinationOptions.Should().Equal(options);
        var update = new UpdateProductVariationRequest { ProductVariationId = entity.Id, CombinationOptions = options, ExpectedConcurrencyStamp = Guid.NewGuid() };
        MemoryPackSerializer.Deserialize<UpdateProductVariationRequest>(MemoryPackSerializer.Serialize(update)).Should().BeEquivalentTo(update);
    }
}
