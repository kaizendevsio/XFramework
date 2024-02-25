using System.ComponentModel.DataAnnotations.Schema;

namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryBranch : BaseModel, IHasOnlineStatus
{
    public Guid LaboratoryId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? UnitNumber { get; set; }

    public string? Street { get; set; }

    public string? Building { get; set; }

    public Guid IdentityCredentialId { get; set; }
    public Guid BarangayId { get; set; }

    public Guid CityId { get; set; }

    public string? Subdivision { get; set; }

    public Guid RegionId { get; set; }

    public bool? MainAddress { get; set; }

    public Guid ProvinceId { get; set; }

    public Guid CountryId { get; set; }

    public int? Status { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? AlternativePhone { get; set; }

    public virtual ICollection<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; } =
        new List<ConsultationJobOrderLaboratory>();

    public virtual Laboratory Laboratory { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<LaboratoryLocationTag> LaboratoryLocationTags { get; set; } =
        new List<LaboratoryLocationTag>();

    public virtual ICollection<LaboratoryMember> LaboratoryMembers { get; set; } = new List<LaboratoryMember>();

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; set; } = new List<LaboratoryService>();

    [NotMapped]
    public List<StorageFile>? Files { get; set; }

    public bool IsOnline { get; set; }
    
    public DateTime LastSeen { get; set; }
    
    public DateTime? OnlineSince { get; set; }
    
    public string? StatusMessage { get; set; }
    
    public string? LastActivityType { get; set; }
    
    public string? Device { get; set; }
    
    public string? Location { get; set; }
}