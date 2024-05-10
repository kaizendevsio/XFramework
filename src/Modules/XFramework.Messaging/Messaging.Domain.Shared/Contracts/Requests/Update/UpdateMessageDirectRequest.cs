using MediatR;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace Messaging.Domain.Shared.Contracts.Requests.Update;

using TResponse = CmdResponse;

public record UpdateMessageDirectRequest : RequestBase,
    ICommand,
    IStreamflowRequest<UpdateMessageDirectRequest, TResponse>
{
    public Guid? Id { get; set; }
}