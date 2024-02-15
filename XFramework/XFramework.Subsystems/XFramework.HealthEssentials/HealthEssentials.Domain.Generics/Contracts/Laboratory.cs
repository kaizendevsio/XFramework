namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Laboratory : BaseModel
{
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

    public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; set; } = new List<HospitalLaboratory>();

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; set; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<LaboratoryLocation> LaboratoryLocations { get; set; } = new List<LaboratoryLocation>();

    public virtual ICollection<LaboratoryMember> LaboratoryMembers { get; set; } = new List<LaboratoryMember>();

    public virtual ICollection<LaboratoryService> LaboratoryServices { get; set; } = new List<LaboratoryService>();

    public virtual ICollection<LaboratoryTag> LaboratoryTags { get; set; } = new List<LaboratoryTag>();
}