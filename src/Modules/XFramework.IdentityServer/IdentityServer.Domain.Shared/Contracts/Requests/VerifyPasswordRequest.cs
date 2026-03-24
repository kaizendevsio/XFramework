namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = VerifyPasswordRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record VerifyPasswordRequest : RequestBase,
    ICommand<CmdResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public string? Password { get; set; }
};