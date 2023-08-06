namespace XFramework.Domain.Generic.Contracts;

public partial class SessionType
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public string? Name { get; set; }

    
    public virtual ICollection<SessionDatum> SessionData { get; } = new List<SessionDatum>();
}
