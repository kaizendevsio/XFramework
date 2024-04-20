using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class CurrencyType : BaseModel
{
    public string? Name { get; set; }

    public string? CurrencyIsoCode3 { get; set; }

    public string? Description { get; set; }

    public short? Type { get; set; }

    public virtual ICollection<AddressCountry> AddressCountries { get; set; } = new List<AddressCountry>();

    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();

    public virtual ICollection<ExchangeRate> ExchangeRateSourceCurrencyTypes { get; set; } = new List<ExchangeRate>();

    public virtual ICollection<ExchangeRate> ExchangeRateTargetCurrencyTypes { get; set; } = new List<ExchangeRate>();

    public virtual ICollection<WalletType> WalletTypes { get; set; } = new List<WalletType>();
}