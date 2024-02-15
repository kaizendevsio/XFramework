namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryServiceTag : BaseModel
{
    public Guid LaboratoryServiceId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual LaboratoryService LaboratoryService { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}