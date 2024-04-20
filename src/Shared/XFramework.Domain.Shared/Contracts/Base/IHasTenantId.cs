namespace XFramework.Domain.Shared.Contracts.Base;

public interface IHasTenantId
{
    public Guid TenantId { get; set; }
}