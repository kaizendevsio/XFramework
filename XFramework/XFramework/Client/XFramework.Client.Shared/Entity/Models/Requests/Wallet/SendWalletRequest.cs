using XFramework.Client.Shared.Entity.Models.Requests.Common;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Client.Shared.Entity.Models.Requests.Wallet;

public class SendWalletRequest : NavigableRequest
{
    public string Recipient { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }
    public string ClientReference { get; set; }
}