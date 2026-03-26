namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record ForgotPasswordRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<ForgotPasswordRequest, TResponse>
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
