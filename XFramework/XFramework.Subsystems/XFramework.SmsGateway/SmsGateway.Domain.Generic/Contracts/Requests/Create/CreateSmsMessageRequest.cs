using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace SmsGateway.Domain.Generic.Contracts.Requests.Create;

public record CreateSmsMessageRequest : TransactionRequestBase, IRequest<CmdResponse<CreateSmsMessageRequest>>
{
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Intent { get; set; }
    public string? Message { get; set; }
    public DateTime? SendSchedule { get; set; }
    public bool IsScheduled { get; set; }
}