namespace XFramework.Domain.Generic.Contracts;

public partial record PaymentGatewayResponseStatusType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; } = new List<PaymentGatewayResponse>();
}