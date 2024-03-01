namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalLaboratory : BaseModel
{
    public Guid HospitalId { get; set; }

    public Guid LaboratoryId { get; set; }


    public virtual Hospital Hospital { get; set; } = null!;

    public virtual Laboratory? Laboratory { get; set; }
}