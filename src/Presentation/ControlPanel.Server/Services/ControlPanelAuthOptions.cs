namespace ControlPanel.Server.Services;

public sealed class ControlPanelAuthOptions
{
    public const string BootstrapAdminSectionName = "ControlPanel:BootstrapAdmin";

    public string UserName { get; set; } = "superadmin";
    public string TenantName { get; set; } = "XFramework Admin";
    public string DisplayName { get; set; } = "Super Admin";
    public string? Password { get; set; }
}

