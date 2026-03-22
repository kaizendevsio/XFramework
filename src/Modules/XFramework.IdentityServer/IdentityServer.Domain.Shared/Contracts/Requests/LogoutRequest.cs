namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record LogoutRequest : RequestBase,
    ICommand<TResponse>,
    IStreamflowRequest<LogoutRequest, TResponse>
{
    public Guid SessionId { get; set; }
    public Guid CredentialId { get; set; }
}
