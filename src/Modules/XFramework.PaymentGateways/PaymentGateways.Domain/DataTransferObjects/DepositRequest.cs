using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class DepositRequest
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public long? IdentityCredentialId { get; set; }
    public long? SourceCurrencyId { get; set; }
    public long? TargetWalletTypeId { get; set; }
    public string Address { get; set; }
    public decimal? Amount { get; set; }
    public string Remarks { get; set; }
    public short? DepositStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string RawRequestData { get; set; }
    public string ReferenceNo { get; set; }
    public string RawResponseData { get; set; }
    public decimal? Discount { get; set; }
    public decimal? ConvenienceFee { get; set; }
    public decimal? SystemFee { get; set; }
    public int? DiscountType { get; set; }
    public long? GatewayId { get; set; }
    public string Guid { get; set; }

    public virtual Gateway Gateway { get; set; }
    public virtual IdentityCredential IdentityCredential { get; set; }
    public virtual CurrencyEntity SourceCurrency { get; set; }
    public virtual WalletEntity TargetWalletType { get; set; }
}