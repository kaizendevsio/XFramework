using System;
using PaymentGateways.Domain.DataTransferObjects;

namespace PaymentGateways.Domain.Contracts.Responses;

public class ProcessTransactionResponse
{
    public Gateway Gateway { get; set; }
    public decimal? Discount { get; set; }
    public decimal Amount { get; set; }
    public decimal SystemFee { get; set; }
    public decimal ConvenienceFee { get; set; }
    public string RawRequestData { get; set; }
    public string RawResponseData { get; set; }
    public string ReferenceNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}