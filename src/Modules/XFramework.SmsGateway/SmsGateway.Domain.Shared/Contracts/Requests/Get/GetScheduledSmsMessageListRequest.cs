using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Get;

using TRequest = GetScheduledSmsMessageListRequest;
using TResponse = QueryResponse<List<SmsNodeJob>>;

[MemoryPackable]
public partial record GetScheduledSmsMessageListRequest : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
}