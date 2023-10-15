namespace XFramework.Domain.Generic.BusinessObjects;

public class GenericAudit
{
    public bool Active { get; set; }
    public bool Enabled { get; set; }
    public Guid Guid { get; set; }
}