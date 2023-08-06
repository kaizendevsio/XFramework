namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyJobOrderConsultationJobOrder
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PharmacyJobOrderId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public string? Remarks { get; set; }

    public short? Status { get; set; }

    
    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual PharmacyJobOrder PharmacyJobOrder { get; set; } = null!;
}
