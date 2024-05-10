using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Get;

using TRequest = GetPendingSmsMessageListRequest;
using TResponse = QueryResponse<List<SmsNodeJob>>;

public record GetPendingSmsMessageListRequest : RequestBase,
    IRequest<TResponse>,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
}