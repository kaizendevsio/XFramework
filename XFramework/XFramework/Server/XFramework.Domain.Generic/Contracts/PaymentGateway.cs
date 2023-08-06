namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGateway
{
    public Guid Id { get; set; }

    public Guid GatewayCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal ServiceCharge { get; set; }

    public string? Image { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public Guid? ProviderEndpointId { get; set; }

    public decimal? Discount { get; set; }

    public decimal ConvenienceFee { get; set; }

    
    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual PaymentGatewayCategory PaymentGatewayCategory { get; set; } = null!;

    public virtual ICollection<PaymentGatewayInstruction> GatewayInstructions { get; } = new List<PaymentGatewayInstruction>();

    public virtual PaymentGatewayEndpoint? ProviderEndpoint { get; set; }
}
