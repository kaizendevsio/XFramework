namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = ResetPasswordRequest;
using TResponse = CmdResponse;

public record ResetPasswordRequest : RequestBase,
    IRequest<TResponse>, 
    IStreamflowRequest<TRequest, TResponse>
{
    public string? PhoneEmailUsername { get; set; }
}