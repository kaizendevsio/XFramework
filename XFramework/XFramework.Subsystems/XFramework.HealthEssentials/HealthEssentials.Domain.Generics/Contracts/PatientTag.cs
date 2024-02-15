namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientTag : BaseModel
{
    public Guid PatientId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Patient Patient { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}