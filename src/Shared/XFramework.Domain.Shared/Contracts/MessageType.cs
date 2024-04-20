using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageType : BaseModel
{
    public string Name { get; set; } = null!;

    public short Priority { get; set; }


    public virtual ICollection<MessageDirect> MessageDirects { get; set; } = new List<MessageDirect>();

    public virtual ICollection<MessageThreadType> MessageThreadTypes { get; set; } = new List<MessageThreadType>();
}