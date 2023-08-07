namespace XFramework.Domain.Generic.Contracts;

public partial class Log
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public short? Severity { get; set; }

    public string? Message { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public string? Initiator { get; set; }

    public short? Type { get; set; }

    public string? Name { get; set; }

    public bool? Seen { get; set; }

    public virtual Application? Application { get; set; }
}
