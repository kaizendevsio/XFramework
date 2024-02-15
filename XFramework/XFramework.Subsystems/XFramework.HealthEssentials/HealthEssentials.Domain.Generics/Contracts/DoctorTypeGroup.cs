namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<DoctorType> DoctorTypes { get; set; } = new List<DoctorType>();
}