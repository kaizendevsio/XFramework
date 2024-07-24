namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record ChangePasswordRequest : RequestBase,
    IRequest<TResponse>, 
    IStreamflowRequest<ChangePasswordRequest, TResponse>
{
    public Guid CreadentialId { get; set; }
    public string? NewPassword { get; set; }
    public Guid VerificationId { get; set; }
    public bool RequireVerificationId { get; set; } = true;
}