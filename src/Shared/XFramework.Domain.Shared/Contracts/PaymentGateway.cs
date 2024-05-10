namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGateway : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid GatewayCategoryId { get; set; }

    [MemoryPackOrder(1)]
    public string Name { get; set; } = null!;

    [MemoryPackOrder(2)]
    public string Description { get; set; } = null!;

    [MemoryPackOrder(3)]
    public decimal ServiceCharge { get; set; }

    [MemoryPackOrder(4)]
    public string? Image { get; set; }


    [MemoryPackOrder(5)]
    public Guid? ProviderEndpointId { get; set; }

    [MemoryPackOrder(6)]
    public decimal? Discount { get; set; }

    [MemoryPackOrder(7)]
    public decimal ConvenienceFee { get; set; }


    [MemoryPackOrder(8)]
    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();

    [MemoryPackOrder(9)]
    public virtual PaymentGatewayCategory PaymentGatewayCategory { get; set; } = null!;

    [MemoryPackOrder(10)]
    public virtual ICollection<PaymentGatewayInstruction> GatewayInstructions { get; set; } =
        new List<PaymentGatewayInstruction>();

    [MemoryPackOrder(11)]
    public virtual PaymentGatewayEndpoint? ProviderEndpoint { get; set; }
}
