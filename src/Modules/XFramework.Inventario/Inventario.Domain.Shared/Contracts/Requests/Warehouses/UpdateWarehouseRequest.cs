using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;

[MemoryPackable]
public partial record UpdateWarehouseRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<UpdateWarehouseRequest, CmdResponse>
{
    public Guid Id { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryCode { get; init; }
}
