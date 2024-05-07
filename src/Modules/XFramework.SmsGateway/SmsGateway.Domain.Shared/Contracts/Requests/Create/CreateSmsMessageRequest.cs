using MediatR;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Create;

public record CreateSmsMessageRequest : TransactionRequestBase, IRequest<CmdResponse<CreateSmsMessageRequest>>
{
    public Guid AgentClusterId { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Intent { get; set; }
    public string? Message { get; set; }
    public DateTime? SendSchedule { get; set; }
    public bool IsScheduled { get; set; }
}