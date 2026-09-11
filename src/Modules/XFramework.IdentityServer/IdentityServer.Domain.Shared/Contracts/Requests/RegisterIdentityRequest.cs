namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse<RegisterIdentityResponse>;

// Workspace and role come from the authenticated application's server-side enrollment policy.
[MemoryPackable]
public partial record RegisterIdentityRequest : RequestBase,
    ICommand<TResponse>, IBoltRequest<RegisterIdentityRequest, TResponse>
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
