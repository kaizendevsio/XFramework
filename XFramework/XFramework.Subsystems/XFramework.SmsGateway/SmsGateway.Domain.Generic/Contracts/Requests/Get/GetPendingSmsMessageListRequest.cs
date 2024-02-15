using MediatR;
using SmsGateway.Domain.Generic.Contracts.Responses.Sms;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Requests;

namespace SmsGateway.Domain.Generic.Contracts.Requests.Get;

public record GetPendingSmsMessageListRequest : RequestBase, IRequest<QueryResponse<List<MessageDirectResponse>>>
{
    
}