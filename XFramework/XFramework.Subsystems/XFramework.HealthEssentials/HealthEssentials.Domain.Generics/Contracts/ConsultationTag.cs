namespace HealthEssentials.Domain.Generics.Contracts;

public partial class ConsultationTag : BaseModel
{
    public Guid ConsultationId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Consultation Consultation { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}