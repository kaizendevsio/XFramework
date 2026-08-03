namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record SetTenantModuleFeaturesRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<SetTenantModuleFeaturesRequest, CmdResponse>
{
    public Guid TenantId { get; set; }
    public Guid ExpectedConcurrencyStamp { get; set; }
    public List<TenantModuleFeatureUpdate> Features { get; set; } = [];
}

[MemoryPackable]
public partial record TenantModuleFeatureUpdate
{
    public string ModuleKey { get; set; } = string.Empty;
    public string SubFeatureKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
}
