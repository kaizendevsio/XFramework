namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineType : BaseModel
{
    public string Name { get; set; } = null!;


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual MedicineTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Medicine> Medicines { get; } = new List<Medicine>();
}