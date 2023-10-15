namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyServiceTag : BaseModel
{
    public Guid PharmacyServiceId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual PharmacyService PharmacyService { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}