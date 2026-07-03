namespace XFramework.Portal.Services;

public sealed class PortalAuthOptions
{
    public const string BootstrapAdminSectionName = "Portal:BootstrapAdmin";

    public string UserName { get; set; } = "superadmin";
    public string TenantName { get; set; } = "Portal Admin";
    public string DisplayName { get; set; } = "Super Admin";
    public string? Password { get; set; }
}
