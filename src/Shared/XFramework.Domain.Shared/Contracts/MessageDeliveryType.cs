using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class MessageDeliveryType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();
}