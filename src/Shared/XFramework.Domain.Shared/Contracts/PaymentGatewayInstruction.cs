namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class PaymentGatewayInstruction : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid? GatewayId { get; set; }

    [MemoryPackOrder(1)]
    public string? InstructionText { get; set; }

    [MemoryPackOrder(2)]
    public string? ExampleText { get; set; }

    [MemoryPackOrder(3)]
    public int? StepOrder { get; set; }

    [MemoryPackOrder(4)]
    public string? Note { get; set; }

    [MemoryPackOrder(5)]
    public virtual PaymentGateway? Gateway { get; set; }
}
