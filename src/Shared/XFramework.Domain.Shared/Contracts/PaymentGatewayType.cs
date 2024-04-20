using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class PaymentGatewayType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<PaymentGatewayEndpoint> GatewayEndpoints { get; set; } = new List<PaymentGatewayEndpoint>();

    public virtual ICollection<PaymentGatewayResponseType> GatewayResponseTypes { get; set; } =
        new List<PaymentGatewayResponseType>();
}