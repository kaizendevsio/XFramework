using XFramework.Domain.Generic.Contracts.Requests;

namespace Wallets.Domain.Generic.Contracts.Requests.Update;

public class ConvertWalletRequest : TransactionRequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? FromWalletEntityGuid { get; set; }
    public Guid? ToWalletEntityGuid { get; set; }
    public decimal? Amount { get; set; }
    public Guid? FromCredentialGuid { get; set; }
    public Guid? ToCredentialGuid { get; set; }
    public string Remarks { get; set; }
}