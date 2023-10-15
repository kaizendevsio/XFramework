namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<MedicineType> MedicineTypes { get; } = new List<MedicineType>();
}