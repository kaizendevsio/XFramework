namespace Wallets.Domain.Generic.Contracts.Responses;

public class WithdrawalRequestResponse
{
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public long IdentityCredentialId { get; set; }
    public string Address { get; set; }
    public decimal? TotalAmount { get; set; }
    public short? WithdrawalStatus { get; set; }
    public string Remarks { get; set; }
    public long? SourceWalletTypeId { get; set; }
    public long? TargetCurrencyId { get; set; }
    public string Guid { get; set; }
}