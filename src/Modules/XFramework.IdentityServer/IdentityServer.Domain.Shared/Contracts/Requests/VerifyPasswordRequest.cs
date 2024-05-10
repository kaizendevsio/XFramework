namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = VerifyPasswordRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record VerifyPasswordRequest : RequestBase,
    ICommand,
    IStreamflowRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public string? Password { get; set; }
};