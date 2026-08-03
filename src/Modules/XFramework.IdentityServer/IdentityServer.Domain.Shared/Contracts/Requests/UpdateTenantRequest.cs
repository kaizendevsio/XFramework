namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UpdateTenantRequest : RequestBase,
    ICommand<CmdResponse<TenantAdministrationResponse>>,
    IBoltRequest<UpdateTenantRequest, CmdResponse<TenantAdministrationResponse>>
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public short? Status { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? AvailabilityDate { get; set; }
    public Guid? ParentTenantId { get; set; }
    public decimal Version { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
}
