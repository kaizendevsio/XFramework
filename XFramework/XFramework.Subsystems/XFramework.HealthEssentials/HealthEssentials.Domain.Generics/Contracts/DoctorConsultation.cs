using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorConsultation : BaseModel
{
    public Guid DoctorId { get; set; }

    public Guid ConsultationId { get; set; }


    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public int Quantity { get; set; }

    public virtual Consultation? Consultation { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;
}