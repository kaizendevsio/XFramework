namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayType
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public virtual ICollection<PaymentGatewayEndpoint> GatewayEndpoints { get; } = new List<PaymentGatewayEndpoint>();

    public virtual ICollection<PaymentGatewayResponseType> GatewayResponseTypes { get; } = new List<PaymentGatewayResponseType>();
}
