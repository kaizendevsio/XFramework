using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Enums;

namespace SmsGateway.Domain.Shared.Contracts.Responses.Sms;

[MemoryPackable]
public partial class SmsNodeJob : BaseModel
{
    public Guid AgentClusterId { get; set; }
    public string? Recipient { get; set; }
    public string? Message { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Queued;
    public string Guid => Id.ToString();
}