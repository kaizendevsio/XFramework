namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceTag : BaseModel
{
    public Guid HospitalServiceId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual HospitalService HospitalService { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}