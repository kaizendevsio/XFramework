using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.Contracts;

public partial class PaymentGatewayInstruction : BaseModel
{
    public Guid? GatewayId { get; set; }

    public string? InstructionText { get; set; }

    public string? ExampleText { get; set; }

    public int? StepOrder { get; set; }

    public string? Note { get; set; }

    public virtual PaymentGateway? Gateway { get; set; }
}