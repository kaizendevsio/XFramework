namespace XFramework.Domain.Generic.Contracts;

public partial record MessageReactionType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Emoji { get; set; } = null!;

    public virtual ICollection<MessageReaction> MessageReactions { get; } = new List<MessageReaction>();
}