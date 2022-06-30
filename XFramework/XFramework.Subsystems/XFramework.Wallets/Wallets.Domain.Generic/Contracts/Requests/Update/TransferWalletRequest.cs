using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Update;

public class TransferWalletRequest : TransactionRequestBase
{
    public string Recipient { get; set; }
    public Guid? CredentialGuid { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }
}