namespace XFramework.Domain.Generic.Contracts;

public partial class MessageDeliveryType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<MessageDelivery> MessageDeliveries { get; set; } = new List<MessageDelivery>();
}