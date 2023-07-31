using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class GatewayInstruction
{
    public long Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? GatewayId { get; set; }
    public string InstructionText { get; set; }
    public string ExampleText { get; set; }
    public int? StepOrder { get; set; }
    public string Note { get; set; }

    public virtual Gateway Gateway { get; set; }
}