namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record SearchIdentitiesResponse
{
    public Guid Id { get; set; }
    public string? HandleName { get; set; }
    public string? Alias { get; set; }
    public string? Tagline { get; set; }
    public int Status { get; set; }
    public Guid TypeId { get; set; }
}
