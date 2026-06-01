namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record UpdateCommunityIdentityRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<UpdateCommunityIdentityRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public Guid Id { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
}
