namespace Messaging.Domain.Shared.Contracts;

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageThreadInvite : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(1)]
    public Guid InvitedCredentialId { get; set; }

    [MemoryPackOrder(2)]
    public Guid InvitedByCredentialId { get; set; }

    [MemoryPackOrder(3)]
    public short Status { get; set; }

    [MemoryPackOrder(4)]
    public DateTime? RespondedAt { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessagePin : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageThreadId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(2)]
    public Guid PinnedByMemberId { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageSaved : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid MessageThreadMemberId { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageReport : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; }

    [MemoryPackOrder(1)]
    public Guid ReporterMemberId { get; set; }

    [MemoryPackOrder(2)]
    public string Reason { get; set; } = string.Empty;

    [MemoryPackOrder(3)]
    public string? Details { get; set; }

    [MemoryPackOrder(4)]
    public short Status { get; set; }
}

[MemoryPackable(GenerateType.CircularReference)]
public partial class MessageBlock : BaseModel
{
    [MemoryPackOrder(0)]
    public Guid BlockerCredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid BlockedCredentialId { get; set; }
}
