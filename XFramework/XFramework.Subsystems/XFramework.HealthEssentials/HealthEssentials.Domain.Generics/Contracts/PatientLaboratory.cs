namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientLaboratory : BaseModel
{
    public Guid PatientId { get; set; }

    public Guid LaboratoryJobOrderId { get; set; }


    public virtual LaboratoryJobOrder? LaboratoryJobOrder { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}