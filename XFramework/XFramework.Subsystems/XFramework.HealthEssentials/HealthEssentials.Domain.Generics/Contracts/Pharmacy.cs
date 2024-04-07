using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Pharmacy : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }
    
    public Guid IdentityId { get; set; }

    public string? ShortName { get; set; }

    public string? Slogan { get; set; }

    public string? Description { get; set; }

    public string? ChemicalComponent { get; set; }


    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public int Status { get; set; }

    public virtual PharmacyType Type { get; set; } = null!;

    public virtual ICollection<PharmacyBranch> PharmacyBranches { get; set; } = new List<PharmacyBranch>();

    public virtual ICollection<PharmacyMember> PharmacyMembers { get; set; } = new List<PharmacyMember>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; set; } = new List<PharmacyService>();
    
    public virtual ICollection<PharmacyTag> PharmacyTags { get; set; } = new List<PharmacyTag>();

    [NotMapped]
    public IdentityCredential? IdentityCredential { get; set; }

    [NotMapped] 
    public ICollection<Wallet>? Wallets => IdentityCredential?.Wallets;
}