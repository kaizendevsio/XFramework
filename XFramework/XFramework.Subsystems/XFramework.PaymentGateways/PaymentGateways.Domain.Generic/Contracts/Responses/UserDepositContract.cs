using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses;

public class UserDepositContract
{
    public long Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? CurrencyId { get; set; }
    public long? TargetWalletTypeId { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string RawRequestData { get; set; }
    public string ReferenceNo { get; set; }
    public string RawResponseData { get; set; }
    public long? GatewayId { get; set; }
        
    public virtual CurrencyEntityContract Currency { get; set; }
    public virtual GatewayContract Gateway { get; set; }
    public virtual WalletEntityContract TargetWalletType { get; set; }
}