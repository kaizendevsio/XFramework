using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

using TRequest = CreateProductRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateProductRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public Guid CategoryId { get; init; }
    public string? SKU { get; init; }
    public string? Brand { get; init; }
    public decimal? Weight { get; init; }
    public string? Image { get; init; }
    public bool? IsAvailable { get; init; }
}
