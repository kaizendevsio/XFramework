namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetTenantFinanceSettingsRequest : RequestBase,
    IBoltRequest<GetTenantFinanceSettingsRequest, QueryResponse<TenantFinanceSettingsResponse>>;

[MemoryPackable]
public partial record UpdateTenantFinanceSettingsRequest : RequestBase,
    IBoltRequest<UpdateTenantFinanceSettingsRequest, QueryResponse<TenantFinanceSettingsResponse>>
{
    public Guid ExpectedConcurrencyStamp { get; set; }
    public TenantFinanceSettings Settings { get; set; } = new();
}
