namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<HospitalType> HospitalTypes { get; } = new List<HospitalType>();
}