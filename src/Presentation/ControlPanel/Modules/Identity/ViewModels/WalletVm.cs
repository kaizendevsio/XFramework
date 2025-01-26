namespace ControlPanel.Modules.Identity.ViewModels;

public class WalletVm
{
    public string WalletType { get; set; } // e.g., "Savings", "Checking"
    public decimal Balance { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public DateTime LastTransactionDate { get; set; }
}
