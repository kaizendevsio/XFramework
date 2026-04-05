namespace Community.Domain.Shared.Contracts.Requests;

public record UpdateCommunityIdentityRequest : RequestBase
{
    public Guid CredentialId { get; set; }
    public Guid Id { get; set; }
    public Guid CommunityIdentityTypeId { get; set; }
}