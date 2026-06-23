using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

using TRequest = CreateProductVariationTypeRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateProductVariationTypeRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? Name { get; init; }
    public string? Code { get; init; }
    public Guid? ProductId { get; init; }
}
