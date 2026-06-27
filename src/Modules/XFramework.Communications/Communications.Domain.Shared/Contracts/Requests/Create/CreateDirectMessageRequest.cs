namespace Communications.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateDirectMessageRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateDirectMessageRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
    public Guid? MessageTypeId { get; set; }
    public DateTime? SendSchedule { get; set; }
    public required MessageTransportType MessageTransportType { get; set; }
    public string Sender { get; set; } = "System";
    public required string Recipient { get; set; } = null!;
    public string? Subject { get; set; }
    public string Intent { get; set; } = "Notification";
    public string? Message { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateKey { get; set; }
    public Dictionary<string, string> TemplateVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsScheduled { get; set; }
}
