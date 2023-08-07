namespace XFramework.Domain.Generic.Contracts;

public partial class CurrencyType
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public string? Name { get; set; }

    public string? CurrencyIsoCode3 { get; set; }

    public string? Description { get; set; }

    public short? Type { get; set; }

    public virtual ICollection<AddressCountry> AddressCountries { get; } = new List<AddressCountry>();

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyTypes { get; } = new List<ExchangeRate>();

    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyTypes { get; } = new List<ExchangeRate>();

    public virtual ICollection<WalletType> WalletTypes { get; } = new List<WalletType>();
}
