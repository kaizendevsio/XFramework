namespace HealthEssentials.Domain.Generics.Contracts;

public partial class AilmentTag : BaseModel
{
    public Guid AilmentId { get; set; }

    public string? Value { get; set; }


    public Guid TagId { get; set; }

    public virtual Ailment Ailment { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}