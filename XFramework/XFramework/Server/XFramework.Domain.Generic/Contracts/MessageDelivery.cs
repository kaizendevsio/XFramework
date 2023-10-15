namespace XFramework.Domain.Generic.Contracts;

public partial record MessageDelivery : BaseModel
{
    public Guid MessageThreadMemberId { get; set; }

    public Guid MessageId { get; set; }

    public Guid TypeId { get; set; }

    public virtual MessageDeliveryType Type { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;

    public virtual MessageThreadMember MessageThreadMember { get; set; } = null!;
}