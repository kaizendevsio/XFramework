namespace XFramework.Domain.Generic.Contracts;

public partial class ExchangeRate
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public Guid SourceCurrencyTypeId { get; set; }

    public Guid TargetCurrencyTypeId { get; set; }

    public decimal? Value { get; set; }

    public decimal? Fee { get; set; }

    public DateTime? EffectivityDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Guid { get; set; }

    public virtual CurrencyType SourceCurrencyType { get; set; } = null!;

    public virtual CurrencyType TargetCurrencyType { get; set; } = null!;
}
