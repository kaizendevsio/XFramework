namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

[MemoryPackable]
public partial record ProductBrandMergePreview(
    Guid CategoryId,
    string SourceBrand,
    string TargetBrand,
    int AffectedProductCount);

