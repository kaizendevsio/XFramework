namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayResponse : BaseModel
{
    
    [MemoryPackOrder(0)]
    public string Code { get; set; } = null!;

    [MemoryPackOrder(1)]
    public string Message { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string Description { get; set; } = null!;

    [MemoryPackOrder(3)]
    public Guid ResponseStatusTypeId { get; set; }

    [MemoryPackOrder(4)]
    public Guid GatewayResponseTypeId { get; set; }

    [MemoryPackOrder(5)]
    public virtual PaymentGatewayResponseType PaymentGatewayResponseType { get; set; } = null!;

    [MemoryPackOrder(6)]
    public virtual PaymentGatewayResponseStatusType ResponseStatusType { get; set; } = null!;
}
