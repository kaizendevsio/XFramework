using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses;

public class CurrencyEntityContract
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string Name { get; set; }
    public string CurrencyIsoCode3 { get; set; }
    public string Description { get; set; }
    public short? CurrencyType { get; set; }
}