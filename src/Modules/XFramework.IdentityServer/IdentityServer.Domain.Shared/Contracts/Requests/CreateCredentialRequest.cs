namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse<CredentialAdministrationResponse>;

[MemoryPackable]
public partial record CreateCredentialRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateCredentialRequest, TResponse>
{
    public Guid IdentityInfoId { get; set; }
    public string? UserName { get; set; }
    public string? UserAlias { get; set; }
    public string? Password { get; set; }
}
