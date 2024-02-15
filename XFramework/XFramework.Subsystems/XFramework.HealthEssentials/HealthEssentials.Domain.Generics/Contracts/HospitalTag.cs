namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalTag : BaseModel
{
    public Guid HospitalId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Hospital Hospital { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}