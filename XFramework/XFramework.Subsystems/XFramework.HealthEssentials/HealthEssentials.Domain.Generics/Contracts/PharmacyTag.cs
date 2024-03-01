namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyTag : BaseModel
{
    public Guid PharmacyId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}