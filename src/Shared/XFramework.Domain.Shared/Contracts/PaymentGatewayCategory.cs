using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class PaymentGatewayCategory : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<PaymentGateway> Gateways { get; set; } = new List<PaymentGateway>();
}