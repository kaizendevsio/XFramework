namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayResponseStatusType : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Code { get; set; } = null!;

    [MemoryPackOrder(2)]
    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; set; } = new List<PaymentGatewayResponse>();
}
