namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineIntake : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ScientificName { get; set; }

    public string? Description { get; set; }

    public Guid Repetition { get; set; }

    public Guid UnitId { get; set; }

    public virtual MedicineIntakeType Type { get; set; } = null!;

    public virtual Unit? Unit { get; set; }
}