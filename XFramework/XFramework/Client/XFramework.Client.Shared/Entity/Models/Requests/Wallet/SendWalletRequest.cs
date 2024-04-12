using XFramework.Client.Shared.Entity.Models.Requests.Common;

namespace XFramework.Client.Shared.Entity.Models.Requests.Wallet;

[SuppressMessage("BlazorState", "TW0001:Blazor State Action should be a nested type of its State")]
public record SendWalletRequest : NavigableRequest
{
    public Guid SenderCredentialId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public Guid WalletTypeId { get; set; }
    public decimal Amount => LineItems.Sum(x => x.AmountDue);
    public bool OnHold { get; set; }
    public decimal TotalAmount => Amount + Fee;
    public decimal Fee => LineItems.Sum(x => x.Fee);
    public string? Remarks { get; set; }
    public string? ClientReference { get; set; }
    public List<(string Description, decimal AmountDue, decimal Fee)> LineItems { get; set; } = [];
}