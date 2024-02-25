namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryLocationTag : BaseModel
{
    public Guid LaboratoryBranchId { get; set; }

    public string? Value { get; set; }

    public Guid TagId { get; set; }


    public virtual LaboratoryBranch LaboratoryBranch { get; set; } = null!;

    public virtual Tag? Tag { get; set; }
}