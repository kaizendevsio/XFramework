namespace XFramework.Inventario.Domain.Shared.Contracts;

public interface IProduct
{
    Guid Id { get; set; }
    string Name { get; set; }
    string Description { get; set; }
    decimal Price { get; set; }
    string? Image { get; set; }
    string? SKU { get; set; }
    string? Brand { get; set; }
    decimal? Weight { get; set; }
    (string Length, string Width, string Height)? Dimensions { get; set; }
    List<string>? Tags { get; set; }
    decimal? Rating { get; set; }
    List<string>? Reviews { get; set; }
    decimal? Discount { get; set; }
    bool IsAvailable { get; set; }
}