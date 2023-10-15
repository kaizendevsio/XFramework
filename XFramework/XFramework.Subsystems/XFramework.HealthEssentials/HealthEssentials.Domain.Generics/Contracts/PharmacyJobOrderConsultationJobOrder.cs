namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyJobOrderConsultationJobOrder : BaseModel
{
    public Guid PharmacyJobOrderId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }


    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual PharmacyJobOrder PharmacyJobOrder { get; set; } = null!;
}