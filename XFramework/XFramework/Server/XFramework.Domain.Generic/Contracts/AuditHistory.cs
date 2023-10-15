namespace XFramework.Domain.Generic.Contracts;

public partial record AuditHistory : BaseModel
{
    public string? TableName { get; set; }


    public string? KeyValues { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? QueryAction { get; set; }
}