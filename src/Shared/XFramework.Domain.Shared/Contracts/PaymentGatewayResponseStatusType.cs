using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class PaymentGatewayResponseStatusType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; set; } = new List<PaymentGatewayResponse>();
}