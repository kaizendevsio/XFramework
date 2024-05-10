namespace Messaging.Domain.Shared.Contracts.Requests.Create;

using TRequest = CreateVerificationMessageRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateVerificationMessageRequest : RequestBase,
    ICommand, 
    IStreamflowRequest<TRequest, TResponse>
{
    public string? VerificationToken { get; set; }
    public GenericContactType ContactType { get; set; }
    public string? Contact { get; set; }
}