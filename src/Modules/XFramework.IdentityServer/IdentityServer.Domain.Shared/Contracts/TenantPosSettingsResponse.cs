using POS.Domain.Shared.Contracts;

namespace IdentityServer.Domain.Shared.Contracts;

[MemoryPackable]
public partial record TenantPosSettingsResponse
{
    public Guid TenantId { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public PosCheckoutSettings Settings { get; set; } = new();
    public bool CanManage { get; set; }
    public bool IsUsable { get; set; }
    public string? ConfigurationError { get; set; }
}
