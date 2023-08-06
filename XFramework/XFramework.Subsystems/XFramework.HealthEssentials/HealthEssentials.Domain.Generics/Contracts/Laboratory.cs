namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Laboratory
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    
    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public int Status { get; set; }

    public virtual LaboratoryType Type { get; set; } = null!;

    public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; } = new List<HospitalLaboratory>();

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<LaboratoryLocation> LaboratoryLocations { get; } = new List<LaboratoryLocation>();

    public virtual ICollection<LaboratoryMember> LaboratoryMembers { get; } = new List<LaboratoryMember>();

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; } = new List<LaboratoryService>();

    public virtual ICollection<LaboratoryTag> LaboratoryTags { get; } = new List<LaboratoryTag>();
}
