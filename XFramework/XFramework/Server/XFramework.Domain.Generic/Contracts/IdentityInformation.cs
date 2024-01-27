using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityInformation : BaseModel
{
    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? IdentityName { get; set; }

    public string? IdentityDescription { get; set; }

    public DateOnly? BirthDate { get; set; }

    public Gender? Gender { get; set; }

    public bool IsVerified { get; set; }

    public CivilStatus? CivilStatus { get; set; }


    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; } = new List<IdentityAddress>();

    public virtual ICollection<IdentityCredential> IdentityCredentials { get; } = new List<IdentityCredential>();
}