using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Get;

using TRequest = GetPendingSmsMessageListRequest;
using TResponse = QueryResponse<List<SmsNodeJob>>;

[MemoryPackable]
public partial record GetPendingSmsMessageListRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid AgentClusterId { get; set; }
}