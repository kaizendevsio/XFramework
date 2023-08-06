namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorConsultation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid DoctorId { get; set; }

    public Guid ConsultationId { get; set; }

    
    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public int Quantity { get; set; }

    public virtual Consultation? Consultation { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;
}
