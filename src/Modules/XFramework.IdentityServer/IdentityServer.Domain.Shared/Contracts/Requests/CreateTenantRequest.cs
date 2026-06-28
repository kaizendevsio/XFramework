namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateTenantRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateTenantRequest, TResponse>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public short? Status { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? AvailabilityDate { get; set; }
    public Guid? ParentTenantId { get; set; }
    public decimal Version { get; set; } = 1.0m;
}
