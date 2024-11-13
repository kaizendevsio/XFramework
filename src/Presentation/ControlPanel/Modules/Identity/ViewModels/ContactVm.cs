namespace ControlPanel.Modules.Identity.ViewModels;


public class ContactVm
{
  
    public string Type { get; set; } // e.g., "Email" or "Phone"
    public string Value { get; set; }
    public bool Verified { get; set; }
    public DateTime CreatedAt { get; set; }
}