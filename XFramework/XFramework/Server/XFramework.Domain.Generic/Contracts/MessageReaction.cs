namespace XFramework.Domain.Generic.Contracts;

public partial class MessageReaction
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long MessageId { get; set; }

    public Guid TypeId { get; set; }

    public virtual MessageReactionType Type { get; set; } = null!;

    public virtual Message Message { get; set; } = null!;
}
