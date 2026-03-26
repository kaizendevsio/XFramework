namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record ResetPasswordRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<ResetPasswordRequest, TResponse>
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}
