namespace XFramework.Domain.Generic.Contracts;

public partial record PaymentGatewayInstruction : BaseModel
{
    public Guid? GatewayId { get; set; }

    public string? InstructionText { get; set; }

    public string? ExampleText { get; set; }

    public int? StepOrder { get; set; }

    public string? Note { get; set; }

    public virtual PaymentGateway? Gateway { get; set; }
}