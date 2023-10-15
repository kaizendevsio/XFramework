namespace XFramework.Domain.Generic.Contracts;

public partial record PaymentGatewayType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<PaymentGatewayEndpoint> GatewayEndpoints { get; } = new List<PaymentGatewayEndpoint>();

    public virtual ICollection<PaymentGatewayResponseType> GatewayResponseTypes { get; } =
        new List<PaymentGatewayResponseType>();
}