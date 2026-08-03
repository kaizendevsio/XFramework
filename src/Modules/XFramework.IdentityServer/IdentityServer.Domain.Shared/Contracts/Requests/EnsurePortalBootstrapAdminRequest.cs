namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record EnsurePortalBootstrapAdminRequest : RequestBase,
    ICommand<CmdResponse<PortalBootstrapAdminResponse>>,
    IBoltRequest<EnsurePortalBootstrapAdminRequest, CmdResponse<PortalBootstrapAdminResponse>>
{
    public string TenantName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
