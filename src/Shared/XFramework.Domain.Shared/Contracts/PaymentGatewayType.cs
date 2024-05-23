namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string? Description { get; set; }


    [MemoryPackOrder(2)]
    public virtual ICollection<PaymentGatewayEndpoint> GatewayEndpoints { get; set; } = new List<PaymentGatewayEndpoint>();

    [MemoryPackOrder(3)]
    public virtual ICollection<PaymentGatewayResponseType> GatewayResponseTypes { get; set; } =
        new List<PaymentGatewayResponseType>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
