using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryMember : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid LaboratoryId { get; set; }

    public string? Value { get; set; }


    public string Name { get; set; } = null!;

    public int Status { get; set; }

    public string? Description { get; set; }

    public Guid LaboratoryBranchId { get; set; }

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual LaboratoryBranch? LaboratoryLocation { get; set; }
    
    [NotMapped]
    public IdentityCredential? Credential { get; set; }
}