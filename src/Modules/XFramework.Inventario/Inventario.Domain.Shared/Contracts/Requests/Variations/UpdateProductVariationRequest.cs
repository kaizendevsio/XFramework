using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

using TRequest = UpdateProductVariationRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateProductVariationRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid ProductVariationId { get; init; }
    public Guid ProductVariationTypeId { get; init; }
    public string? Name { get; init; }
    public decimal Price { get; init; }
}
