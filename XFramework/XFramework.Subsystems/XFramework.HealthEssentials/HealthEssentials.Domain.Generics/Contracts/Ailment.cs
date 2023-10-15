namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Ailment : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? OtherName { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<AilmentTag> AilmentTags { get; } = new List<AilmentTag>();

    public virtual AilmentType Type { get; set; } = null!;

    public virtual ICollection<PatientAilment> PatientAilments { get; } = new List<PatientAilment>();
}