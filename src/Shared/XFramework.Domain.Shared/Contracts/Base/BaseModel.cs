namespace XFramework.Domain.Shared.Contracts.Base;

public abstract class BaseModel : ISoftDeletable, IHasId, IHasConcurrencyStamp, IAuditable, IHasTenantId
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid TenantId { get; set; }
}