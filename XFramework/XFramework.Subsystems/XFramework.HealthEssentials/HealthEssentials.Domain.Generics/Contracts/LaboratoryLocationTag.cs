namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryLocationTag : BaseModel
{
    public Guid LaboratoryLocationId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual LaboratoryLocation LaboratoryLocation { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}