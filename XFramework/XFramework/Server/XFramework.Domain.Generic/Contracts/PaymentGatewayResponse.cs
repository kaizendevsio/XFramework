namespace XFramework.Domain.Generic.Contracts;

public partial class PaymentGatewayResponse : BaseModel
{
    public string Code { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Description { get; set; } = null!;

    public Guid ResponseStatusTypeId { get; set; }

    public Guid GatewayResponseTypeId { get; set; }

    public virtual PaymentGatewayResponseType PaymentGatewayResponseType { get; set; } = null!;

    public virtual PaymentGatewayResponseStatusType ResponseStatusType { get; set; } = null!;
}