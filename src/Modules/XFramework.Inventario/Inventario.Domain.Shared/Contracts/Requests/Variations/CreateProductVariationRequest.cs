using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

using TRequest = CreateProductVariationRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateProductVariationRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductId { get; init; }
    public Guid ProductVariationTypeId { get; init; }
    public string? Name { get; init; }
    public decimal Price { get; init; }
}
