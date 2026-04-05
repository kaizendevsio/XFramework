namespace Community.Domain.Shared.Contracts.Requests;

public record GetCommunityConnectionListRequest : RequestBase
{
    public Guid ConnectionTypeId { get; set; }
    public Guid CommunityIdentityId { get; set; }
    public int Limit { get; set; }
}