namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<HospitalServiceType> HospitalServiceTypes { get; } = new List<HospitalServiceType>();
}