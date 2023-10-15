namespace XFramework.Domain.Generic.Contracts;

public partial record Log : BaseModel
{
    public Guid TenantId { get; set; }

    public short? Severity { get; set; }

    public string? Message { get; set; }


    public string? Initiator { get; set; }

    public short? Type { get; set; }

    public string? Name { get; set; }

    public bool? Seen { get; set; }

    public virtual Tenant? Application { get; set; }
}