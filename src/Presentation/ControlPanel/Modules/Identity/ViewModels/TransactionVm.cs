namespace ControlPanel.Modules.Identity.ViewModels;

public class TransactionVm
{
  
    public string Type { get; set; } // e.g., "Cash-in", "Cash-out", "Transfer"
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; }
}