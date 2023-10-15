namespace XFramework.Domain.Generic.Contracts;

public partial record MessageReaction : BaseModel
{
    public Guid MessageId { get; set; }

    public Guid TypeId { get; set; }

    public virtual MessageReactionType Type { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}