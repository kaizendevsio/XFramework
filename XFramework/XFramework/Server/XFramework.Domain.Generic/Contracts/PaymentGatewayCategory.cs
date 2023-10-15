namespace XFramework.Domain.Generic.Contracts;

public partial record PaymentGatewayCategory : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<PaymentGateway> Gateways { get; } = new List<PaymentGateway>();
}