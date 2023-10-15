namespace XFramework.Domain.Generic.Contracts;

public partial record PaymentGatewayResponseType : BaseModel
{
    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public Guid GatewayTypeId { get; set; }

    public virtual PaymentGatewayType PaymentGatewayType { get; set; } = null!;

    public virtual ICollection<PaymentGatewayResponse> GatewayResponses { get; } = new List<PaymentGatewayResponse>();
}