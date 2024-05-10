namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = ResetPasswordRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ResetPasswordRequest : RequestBase,
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public string? PhoneEmailUsername { get; set; }
}