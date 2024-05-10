using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Get;

using TRequest = GetScheduledSmsMessageListRequest;
using TResponse = QueryResponse<List<SmsNodeJob>>;

public record GetScheduledSmsMessageListRequest : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
}