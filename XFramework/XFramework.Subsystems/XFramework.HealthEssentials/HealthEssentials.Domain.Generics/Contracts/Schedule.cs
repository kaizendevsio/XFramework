namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Schedule : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public short? Status { get; set; }

    public Guid PriorityId { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime ExpireAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }


    public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; } = new List<ConsultationJobOrder>();

    public virtual ScheduleType Type { get; set; } = null!;

    public virtual ICollection<LaboratoryJobOrder> LaboratoryJobOrders { get; } = new List<LaboratoryJobOrder>();

    public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; } = new List<LogisticJobOrder>();

    public virtual ICollection<PharmacyJobOrder> PharmacyJobOrders { get; } = new List<PharmacyJobOrder>();

    public virtual SchedulePriority Priority { get; set; } = null!;

    public virtual ICollection<ScheduleTag> ScheduleTags { get; } = new List<ScheduleTag>();
}