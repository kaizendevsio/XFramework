namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineIntakeType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public int? SortOrder { get; set; }

    public virtual ICollection<MedicineIntake> MedicineIntakes { get; } = new List<MedicineIntake>();
}
