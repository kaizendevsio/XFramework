namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineTag : BaseModel
{
    public Guid MedicineId { get; set; }

    public string? Value { get; set; }


    public Guid TagId { get; set; }

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Tag Tag { get; set; } = null!;
}