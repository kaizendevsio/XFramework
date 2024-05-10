using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Messaging.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateVerificationMessageRequest;
using TResponse = CmdResponse;

public record CreateVerificationMessageRequest : RequestBase,
    ICommand, 
    IStreamflowRequest<TRequest, TResponse>
{
    public string? VerificationToken { get; set; }
    public GenericContactType ContactType { get; set; }
    public string? Contact { get; set; }
}