namespace ControlPanel.Modules.Identity.ViewModels;

public class SessionVm
{
  
    public string IpAddress { get; set; }
    public string Device { get; set; }
    public string Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}