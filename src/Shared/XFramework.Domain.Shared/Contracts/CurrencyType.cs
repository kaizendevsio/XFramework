namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class CurrencyType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string? Name { get; set; }

    [MemoryPackOrder(1)]
    public string? CurrencyIsoCode3 { get; set; }

    [MemoryPackOrder(2)]
    public string? Description { get; set; }

    [MemoryPackOrder(3)]
    public short? Type { get; set; }

    [MemoryPackOrder(4)]
    public virtual ICollection<AddressCountry> AddressCountries { get; set; } = new List<AddressCountry>();

    [MemoryPackOrder(5)]
    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();

    [MemoryPackOrder(6)]
    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyTypes { get; set; } = new List<ExchangeRate>();

    [MemoryPackOrder(7)]
    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyTypes { get; set; } = new List<ExchangeRate>();

    [MemoryPackOrder(8)]
    public virtual ICollection<WalletType> WalletTypes { get; set; } = new List<WalletType>();
}
