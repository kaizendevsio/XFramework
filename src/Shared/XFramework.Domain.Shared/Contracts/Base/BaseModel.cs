namespace XFramework.Domain.Shared.Contracts.Base;


public abstract partial class BaseModel : ISoftDeletable, IHasId, IHasConcurrencyStamp, IAuditable, IHasTenantId
{
    [MemoryPackOrder(100)]
    public Guid Id { get; set; }
    [MemoryPackOrder(101)]
    public bool IsEnabled { get; set; }
    [MemoryPackOrder(102)]
    public bool IsDeleted { get; set; }
    [MemoryPackOrder(103)]
    public Guid ConcurrencyStamp { get; set; }
    [MemoryPackOrder(104)]
    public DateTime CreatedAt { get; set; }
    [MemoryPackOrder(105)]
    public DateTime? ModifiedAt { get; set; }
    [MemoryPackOrder(106)]
    public DateTime? DeletedAt { get; set; }
    [MemoryPackOrder(107)]
    public Guid TenantId { get; set; }
}