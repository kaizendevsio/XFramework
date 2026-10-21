using POS.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetTenantPosSettingsRequest : RequestBase,
    IBoltRequest<GetTenantPosSettingsRequest, QueryResponse<TenantPosSettingsResponse>>;

[MemoryPackable]
public partial record UpdateTenantPosSettingsRequest : RequestBase,
    IBoltRequest<UpdateTenantPosSettingsRequest, QueryResponse<TenantPosSettingsResponse>>
{
    public Guid ExpectedConcurrencyStamp { get; set; }
    public PosCheckoutSettings Settings { get; set; } = new();
}
