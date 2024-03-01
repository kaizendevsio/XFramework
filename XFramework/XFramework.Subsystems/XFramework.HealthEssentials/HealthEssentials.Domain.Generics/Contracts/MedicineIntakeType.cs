namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineIntakeType : BaseModel
{
    public string Name { get; set; } = null!;


    public int? SortOrder { get; set; }

    public virtual ICollection<MedicineIntake> MedicineIntakes { get; set; } = new List<MedicineIntake>();
}