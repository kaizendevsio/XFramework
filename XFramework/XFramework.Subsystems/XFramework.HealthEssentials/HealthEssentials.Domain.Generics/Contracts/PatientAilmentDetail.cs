namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientAilmentDetail : BaseModel
{
    public Guid PatientAilmentId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public string? DoctorName { get; set; }

    public short? Status { get; set; }

    public string? Remarks { get; set; }


    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual PatientAilment PatientAilment { get; set; } = null!;
}