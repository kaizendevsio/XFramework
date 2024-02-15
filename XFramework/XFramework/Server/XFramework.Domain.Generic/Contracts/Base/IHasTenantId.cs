namespace XFramework.Domain.Generic.Contracts.Base;

public interface IHasTenantId
{
    public Guid TenantId { get; set; }
}