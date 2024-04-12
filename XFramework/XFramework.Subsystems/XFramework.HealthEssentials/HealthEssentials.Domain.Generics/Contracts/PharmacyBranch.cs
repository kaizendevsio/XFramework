using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyBranch : BaseModel, IHasOnlineStatus
{
    public Guid PharmacyId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public Guid BarangayId { get; set; }

    public Guid CityId { get; set; }

    public string? Subdivision { get; set; }

    public Guid RegionId { get; set; }

    public bool? MainAddress { get; set; }

    public Guid ProvinceId { get; set; }

    public Guid CountryId { get; set; }

    public Guid IdentityId { get; set; }

    public int? Status { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? AlternativePhone { get; set; }

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; set; } = new List<PharmacyJobOrder>();

    public virtual ICollection<PharmacyMember> PharmacyMembers { get; set; } = new List<PharmacyMember>();

    public virtual ICollection<PharmacyService> PharmacyServices { get; set; } = new List<PharmacyService>();

    public virtual ICollection<PharmacyStock> PharmacyStocks { get; set; } = new List<PharmacyStock>();
        
    [NotMapped]
    public List<StorageFile>? Files { get; set; }
    
    [NotMapped]
    public IdentityCredential? IdentityCredential { get; set; }

    [NotMapped] 
    public ICollection<Wallet>? Wallets => IdentityCredential?.Wallets;
    
    [NotMapped]
    public IdentityAddress? Address => IdentityCredential?.IdentityInfo?.IdentityAddresses.FirstOrDefault();
    
    public bool IsOnline { get; set; }
    
    public DateTime LastSeen { get; set; }
    
    public DateTime? OnlineSince { get; set; }
    
    public string? StatusMessage { get; set; }
    
    public string? LastActivityType { get; set; }
    
    public string? Device { get; set; }
    
    public string? Location { get; set; }
}