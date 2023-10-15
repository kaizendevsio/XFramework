namespace XFramework.Domain.Generic.Contracts;

public partial record MessageDeliveryType : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<MessageDelivery> MessageDeliveries { get; } = new List<MessageDelivery>();
}