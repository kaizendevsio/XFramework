using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Create;

public class CreateWalletWithdrawalRequest : TransactionRequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? GatewayGuid { get; set; }
    public Guid? WalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
}