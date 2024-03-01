namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorTag : BaseModel
{
    public Guid DoctorId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Doctor Doctor { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}