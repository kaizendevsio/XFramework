namespace XFramework.Client.Shared.Entity.Models.Requests.Wallet;

public class SendWalletRequest
{
    public string Recipient { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }   
}