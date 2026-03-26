namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetThreadResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Guid TypeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ThreadMemberResponse> Members { get; set; } = [];
}
