using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses;

public class DepositResponse
{
    public string? GatewayName { get; set; }
    public string? Result { get; set; }
    public object? Transaction { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal ConvenienceFee { get; set; }
    public decimal Discount { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}