using XFramework.Domain.Shared.Contracts;

namespace ControlPanel.Modules.Identity.ViewModels;

public class RoleVm
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Permissions { get; set; }
    public int NumberOfUsers { get; set; }
    public DateTime CreatedAt { get; set; }
}