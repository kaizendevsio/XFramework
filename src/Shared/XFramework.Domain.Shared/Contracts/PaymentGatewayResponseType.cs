namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayResponseType : BaseModel, IHasSystemReferenceId
{
    
    [MemoryPackOrder(0)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Code { get; set; } = null!;

    [MemoryPackOrder(2)]
    public Guid GatewayTypeId { get; set; }

    [MemoryPackOrder(3)]
    public virtual PaymentGatewayType PaymentGatewayType { get; set; } = null!;

    [MemoryPackOrder(4)]
    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; set; } = new List<PaymentGatewayResponse>();

    [MemoryPackOrder(200)]
    public Guid SystemReferenceId { get; set; }
}
