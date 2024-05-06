namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = VerifyPasswordRequest;
using TResponse = CmdResponse;

public record VerifyPasswordRequest : RequestBase,
    ICommand,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public string? Password { get; set; }
};