using MediatR;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace SmsGateway.Domain.Shared.Contracts.Requests.Get;

public record GetScheduledSmsMessageListRequest : RequestBase, IRequest<QueryResponse<List<MessageDirectResponse>>>
{
    
}