namespace XFramework.Domain.Generic.Contracts;

public partial class MessageDelivery
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    
    public Guid MessageThreadMemberId { get; set; }

    public Guid MessageId { get; set; }

    public Guid TypeId { get; set; }

    public virtual MessageDeliveryType Type { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}
