namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayEndpoint
{
    public Guid Id { get; set; }

    public Guid? GatewayId { get; set; }

    public string? Name { get; set; }

    public string? BaseUrlEndpoint { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public string? UrlEndpoint { get; set; }

    
    public virtual PaymentGatewayType? Gateway { get; set; }

    public virtual ICollection<PaymentGateway> Gateways { get; } = new List<PaymentGateway>();
}
