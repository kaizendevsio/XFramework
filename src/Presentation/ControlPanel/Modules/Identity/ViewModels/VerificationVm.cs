namespace ControlPanel.Modules.Identity.ViewModels;

public class VerificationVm
{
  
    public string Type { get; set; } // e.g., "Email" or "Phone"
    public string Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime VerifiedAt { get; set; }
}