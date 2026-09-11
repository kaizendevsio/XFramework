using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

[MemoryPackable]
public partial record UpdateSupplierRequest : RequestBase, ICommand<CmdResponse>, IBoltRequest<UpdateSupplierRequest, CmdResponse>
{
    public Guid Id { get; init; }
    public Guid ConcurrencyStamp { get; init; }
    public string? Name { get; init; }
    public string? ContactName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
}
