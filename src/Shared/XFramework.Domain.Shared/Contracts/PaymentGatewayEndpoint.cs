namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayEndpoint : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? GatewayId { get; set; }

    [MemoryPackOrder(1)]
    public string? Name { get; set; }

    [MemoryPackOrder(2)]
    public string? BaseUrlEndpoint { get; set; }


    [MemoryPackOrder(3)]
    public string? UrlEndpoint { get; set; }


    [MemoryPackOrder(4)]
    public virtual PaymentGatewayType? Gateway { get; set; }

    [MemoryPackOrder(5)]
    public virtual ICollection<PaymentGateway> Gateways { get; set; } = new List<PaymentGateway>();
}
