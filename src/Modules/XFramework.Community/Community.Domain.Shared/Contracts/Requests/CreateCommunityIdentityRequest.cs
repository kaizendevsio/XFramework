namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record CreateCommunityIdentityRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateCommunityIdentityRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
    public string? Tagline { get; set; }
    public string? Alias { get; set; }
    public string? HandleName { get; set; }
}
