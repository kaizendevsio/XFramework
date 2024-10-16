namespace Fluid.Core.Models;

public abstract class BaseModel
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