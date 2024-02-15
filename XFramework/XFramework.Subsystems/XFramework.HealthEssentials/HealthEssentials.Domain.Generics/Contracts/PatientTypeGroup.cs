namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<PatientType> PatientTypes { get; set; } = new List<PatientType>();
}