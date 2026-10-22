using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Portal.Features.Inventario;

public sealed class ProductVariationCombinationDraft
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public List<Option> Options { get; } = [new(), new()];

    public List<ProductVariationOption> ToOptions() => Options.Select(x => new ProductVariationOption
    {
        ProductVariationTypeId = x.TypeId,
        Value = x.Value.Trim()
    }).ToList();

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 200)
            return "Enter a combination name of at most 200 characters.";
        if (Price < 0 || Price > ProductVariationCombination.MaxPrice || decimal.Round(Price, 2) != Price)
            return "Enter a non-negative price with at most two decimal places.";
        return ProductVariationCombination.Validate(ToOptions());
    }

    public void Load(string? name, decimal price, IEnumerable<ProductVariationOption>? options = null)
    {
        Name = name ?? "";
        Price = price;
        Options.Clear();
        if (options is not null)
            Options.AddRange(options.Select(x => new Option { TypeId = x.ProductVariationTypeId, Value = x.Value }));
        while (Options.Count < 2)
            Options.Add(new());
    }

    public sealed class Option
    {
        public Guid TypeId { get; set; }
        public string Value { get; set; } = "";
    }
}
