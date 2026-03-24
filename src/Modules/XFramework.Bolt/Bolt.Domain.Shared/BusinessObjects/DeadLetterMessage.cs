using MessagePack;

namespace Bolt.Domain.Shared.BusinessObjects;

[MessagePackObject]
public class DeadLetterMessage
{
    [Key(0)] public Guid RequestId { get; set; }
    [Key(1)] public string CommandName { get; set; }
    [Key(2)] public string RecipientId { get; set; }
    [Key(3)] public string SenderId { get; set; }
    [Key(4)] public ReadOnlyMemory<byte> Data { get; set; }
    [Key(5)] public string DropReason { get; set; }
    [Key(6)] public int RetryCount { get; set; }
    [Key(7)] public DateTime DroppedAt { get; set; }
    [Key(8)] public string? ErrorMessage { get; set; }
}
