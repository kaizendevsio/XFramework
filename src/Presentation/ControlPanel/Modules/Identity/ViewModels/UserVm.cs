using XFramework.Domain.Shared.Contracts;

namespace ControlPanel.Modules.Identity.ViewModels;

public class UserVm
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
    public string Role { get; set; }
    public DateTime LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
}