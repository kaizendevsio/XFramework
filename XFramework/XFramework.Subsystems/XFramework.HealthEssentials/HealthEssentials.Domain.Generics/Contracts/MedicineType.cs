namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual MedicineTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Medicine> Medicines { get; } = new List<Medicine>();
}
