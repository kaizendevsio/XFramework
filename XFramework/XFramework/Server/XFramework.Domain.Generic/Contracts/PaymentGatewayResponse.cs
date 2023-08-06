namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayResponse
{
    public Guid Id { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Description { get; set; } = null!;

    public Guid ResponseStatusTypeId { get; set; }

    public Guid GatewayResponseTypeId { get; set; }

    public virtual PaymentGatewayResponseType PaymentGatewayResponseType { get; set; } = null!;

    public virtual PaymentGatewayResponseStatusType ResponseStatusType { get; set; } = null!;
}
