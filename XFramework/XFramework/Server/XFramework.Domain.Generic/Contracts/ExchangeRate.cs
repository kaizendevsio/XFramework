namespace XFramework.Domain.Generic.Contracts;

public partial record ExchangeRate : BaseModel
{
    public Guid SourceCurrencyTypeId { get; set; }

    public Guid TargetCurrencyTypeId { get; set; }

    public decimal? Value { get; set; }

    public decimal? Fee { get; set; }

    public DateTime? EffectivityDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public virtual CurrencyType SourceCurrencyType { get; set; } = null!;

    public virtual CurrencyType TargetCurrencyType { get; set; } = null!;
}