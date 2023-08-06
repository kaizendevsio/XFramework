namespace XFramework.Domain.Generic.Contracts;

public partial class AuditHistory
{
    public Guid Id { get; set; }

    public string? TableName { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? KeyValues { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? QueryAction { get; set; }

    }
