using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Enums;

namespace XFramework.Domain.Shared.Contracts;

public partial class IdentityInformation : BaseModel
{
    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? Suffix { get; set; }
    
    [NotMapped]
    [JsonIgnore]
    public string? FullName => string.Join(" ", FirstName, MiddleName, LastName, Suffix);
    
    public string? IdentityName { get; set; }

    public string? IdentityDescription { get; set; }

    public DateOnly? BirthDate { get; set; }

    public Gender? Gender { get; set; }

    public bool IsVerified { get; set; }

    public CivilStatus? CivilStatus { get; set; }


    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; } = new List<IdentityAddress>();

    public virtual ICollection<IdentityCredential> IdentityCredentials { get; set; } = new List<IdentityCredential>();
}