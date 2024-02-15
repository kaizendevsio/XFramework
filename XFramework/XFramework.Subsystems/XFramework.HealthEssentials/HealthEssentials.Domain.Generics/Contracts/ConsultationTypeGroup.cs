namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<ConsultationType> ConsultationTypes { get; set; } = new List<ConsultationType>();
}