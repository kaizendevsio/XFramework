namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetCommunityIdentityResponse
{
    public Guid Id { get; set; }
    public string? HandleName { get; set; }
    public string? Tagline { get; set; }
    public string? Alias { get; set; }
    public int Status { get; set; }
    public DateTime LastActive { get; set; }
    public Guid TypeId { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int ContentCount { get; set; }
}
