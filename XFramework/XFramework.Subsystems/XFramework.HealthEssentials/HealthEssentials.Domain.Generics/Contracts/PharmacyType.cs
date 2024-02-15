namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyType : BaseModel
{
    public string Name { get; set; } = null!;


    public int? SortOrder { get; set; }

    public virtual ICollection<Pharmacy> Pharmacies { get; set; } = new List<Pharmacy>();
}