namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CreateThreadResponse
{
    public Guid ThreadId { get; set; }
}
