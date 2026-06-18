using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

using TRequest = CreateWarehouseRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateWarehouseRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
    public bool IsDefault { get; init; }
}
