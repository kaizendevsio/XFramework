namespace XFramework.Blazor.Entity.Models.Requests.Wallet;

public class SendWalletLineItem
{
    public decimal? Amount { get; set; }
    public decimal Fee { get; set; }
    public string? Description { get; set; }
}