namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayResponseStatusType
{
    public Guid Id { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; } = new List<PaymentGatewayResponse>();
}
