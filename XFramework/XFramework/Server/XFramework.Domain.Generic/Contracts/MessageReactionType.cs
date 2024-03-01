namespace XFramework.Domain.Generic.Contracts;

public partial class MessageReactionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Emoji { get; set; } = null!;

    public virtual ICollection<MessageReaction> MessageReactions { get; set; } = new List<MessageReaction>();
}