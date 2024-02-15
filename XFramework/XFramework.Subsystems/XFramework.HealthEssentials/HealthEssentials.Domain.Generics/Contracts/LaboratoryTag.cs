namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryTag : BaseModel
{
    public Guid LaboratoryId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}