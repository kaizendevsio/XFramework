namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;

[MemoryPackable]
public partial record SellableProductCatalogItem(
    Guid ProductId,
    Guid? ProductVariationId,
    string DisplayName,
    string ProductName,
    string? VariantName,
    Guid? ProductVariationTypeId,
    string? VariantTypeName,
    string? SKU,
    string? Brand,
    string? Image,
    Guid CategoryId,
    string? CategoryName,
    bool IsAvailable,
    decimal Price);

[MemoryPackable]
public partial record SellableProductDetail(
    Guid ProductId,
    string Name,
    string? Description,
    string? SKU,
    string? Brand,
    string? Image,
    Guid CategoryId,
    string? CategoryName,
    bool IsAvailable,
    decimal Price,
    List<SellableProductVariationItem> Variations);

[MemoryPackable]
public partial record SellableProductVariationItem(
    Guid ProductVariationId,
    Guid ProductId,
    Guid? ProductVariationTypeId,
    string? VariantTypeName,
    string VariantName,
    decimal Price,
    decimal BaseProductPrice,
    decimal PriceDelta);
