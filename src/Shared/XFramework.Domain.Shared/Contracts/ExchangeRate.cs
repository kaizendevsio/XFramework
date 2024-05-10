namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class ExchangeRate : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid SourceCurrencyTypeId { get; set; }

    [MemoryPackOrder(1)]
    public Guid TargetCurrencyTypeId { get; set; }

    [MemoryPackOrder(2)]
    public decimal? Value { get; set; }

    [MemoryPackOrder(3)]
    public decimal? Fee { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? EffectivityDate { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? ExpiryDate { get; set; }

    [MemoryPackOrder(6)]
    public virtual CurrencyType SourceCurrencyType { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual CurrencyType TargetCurrencyType { get; set; } = null!;
}
