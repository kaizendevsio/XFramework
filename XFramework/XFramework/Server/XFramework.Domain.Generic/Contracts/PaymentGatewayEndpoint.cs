namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayEndpoint : BaseModel
{
    public Guid? GatewayId { get; set; }

    public string? Name { get; set; }

    public string? BaseUrlEndpoint { get; set; }


    public string? UrlEndpoint { get; set; }


    public virtual PaymentGatewayType? Gateway { get; set; }

    public virtual ICollection<PaymentGateway> Gateways { get; } = new List<PaymentGateway>();
}