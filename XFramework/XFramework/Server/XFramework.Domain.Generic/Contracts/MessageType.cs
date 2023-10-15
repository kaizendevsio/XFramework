namespace XFramework.Domain.Generic.Contracts;

public partial record MessageType : BaseModel
{
    public string Name { get; set; } = null!;

    public short Priority { get; set; }


    public virtual ICollection<MessageDirect> MessageDirects { get; } = new List<MessageDirect>();

    public virtual ICollection<MessageThreadType> MessageThreadTypes { get; } = new List<MessageThreadType>();
}